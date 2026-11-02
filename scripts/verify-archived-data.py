"""Restore a read-only audit source from the supplied archive; never overwrite a database."""
import os, json, subprocess, uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PG = Path(os.environ.get('CINEMACITY_PG_BIN', 'E:/PostgreSQL/18/bin'))
password = os.environ['PGPASSWORD']
env = dict(os.environ, PGPASSWORD=password)
names = ['cinemacity_e2e_audit_' + kind + '_' + uuid.uuid4().hex for kind in ['source','auth','catalog','community']]
def query(db, sql):
    return subprocess.check_output([str(PG/'psql.exe'),'-h','127.0.0.1','-p','5432','-U','postgres','-d',db,'-At','-v','ON_ERROR_STOP=1','-c',sql],env=env).decode('utf-8').strip()
def values(db, schema, table, columns):
    return json.loads(query(db, f"SELECT coalesce(jsonb_agg(to_jsonb(t)), '[]'::jsonb)::text FROM (SELECT {columns} FROM {schema}.{table} ORDER BY id) t"))
try:
    subprocess.run([str(PG/'createdb.exe'),'-h','127.0.0.1','-U','postgres',names[0]], env=env, check=True)
    subprocess.run([str(PG/'pg_restore.exe'),'-h','127.0.0.1','-U','postgres','--no-owner','--no-privileges','-d',names[0],str(ROOT/'.local/backups/cinemacity-20261015-191210.dump')],env=env,check=True,stdout=subprocess.DEVNULL)
    for key, db in zip(['SOURCE','AUTH','CATALOG','COMMUNITY'], names):
        env['CINEMACITY_' + key + '_CONNECTION'] = f'Host=127.0.0.1;Port=5432;Database={db};Username=postgres;Password={password}'
    env.pop('CINEMACITY_MONOLITH_CONNECTION',None)
    run = subprocess.run(['dotnet',str(ROOT/'tools/CinemaCity.DataMigration/.build/bin/Debug/net10.0/CinemaCity.DataMigration.dll')],env=env,capture_output=True,text=True)
    if run.returncode:
        # Do not print connection strings, account records or password hashes.
        raise RuntimeError('Archived-data importer failed; inspect a controlled local run for diagnostics.')
    source_schema = query(names[0], "SELECT CASE WHEN to_regclass('legacy.movies') IS NOT NULL THEN 'legacy' WHEN to_regclass('public.movies') IS NOT NULL THEN 'public' ELSE 'projections' END")
    users = values(names[0], source_schema, 'users', 'id, password_hash')
    assert users == values(names[1], 'public', 'users', 'id, password_hash'), 'Account IDs/hashes changed'
    movies = values(names[0], source_schema, 'movies', 'id')
    assert movies == values(names[2], 'public', 'movies', 'id'), 'Catalog UUIDs changed'
    counts = {}
    for table in ['ratings','reviews','comments','watchlist_items','user_movie_lists','user_movie_list_items']:
        original = values(names[0], 'public', table, 'id')
        assert original == values(names[3], 'public', table, 'id'), table + ' UUIDs changed'
        counts[table] = len(original)
    report = {'result':'passed', 'archive':'cinemacity-20261015-191210.dump','accounts_with_identical_ids_and_hashes':len(users),'catalog_ids_preserved':len(movies),'community_ids_preserved':counts,'working_databases_modified':False}
    (ROOT/'.logs/lab4-archived-data.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report,indent=2))
finally:
    for name in reversed(names):
        assert name.startswith('cinemacity_e2e_audit_')
        query('postgres', f'DROP DATABASE IF EXISTS "{name}" WITH (FORCE)')
