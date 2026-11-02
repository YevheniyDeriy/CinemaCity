import React from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { api, ApiError } from '../api/client';
import { ClientCatalog, ClientDetails, ClientRatingForm } from './ClientPages';
afterEach(() => { cleanup(); vi.restoreAllMocks(); });
function form() { render(<MemoryRouter initialEntries={['/movies/movie/rate']}><Routes><Route path="/movies/:id/rate" element={<ClientRatingForm />} /><Route path="/my-ratings" element={<p>Оцінку збережено</p>} /></Routes></MemoryRouter>); }
it('retains one key after a network failure and rejects duplicate button submissions', async () => {
  let resolvePost: (value: unknown) => void = () => {};
  const keys: string[] = []; let attempts = 0;
  vi.spyOn(api, 'request').mockImplementation(async (_path, init) => {
    if (!init?.method) return { items: [] } as never;
    keys.push(new Headers(init.headers).get('Idempotency-Key')!); attempts++;
    if (attempts === 1) throw new ApiError(0, 'Немає зв’язку із сервером.');
    return await new Promise<unknown>(resolve => { resolvePost = resolve; }) as never;
  });
  form(); const input = await screen.findByRole('spinbutton'); fireEvent.change(input, { target: { value: '8' } });
  fireEvent.submit(input.closest('form')!); await screen.findByText('Немає зв’язку із сервером.');
  fireEvent.submit(input.closest('form')!); fireEvent.submit(input.closest('form')!);
  await waitFor(() => expect(attempts).toBe(2)); expect(keys[0]).toBe(keys[1]);
  resolvePost({}); await screen.findByText('Оцінку збережено');
});
it('shows validation beside the score field', async () => {
  vi.spyOn(api, 'request').mockImplementation(async (_path, init) => {
    if (!init?.method) return { items: [] } as never;
    throw new ApiError(400, 'Перевірте введені дані.', { score: ['Відхилено сервером.'] });
  });
  form(); const input = await screen.findByRole('spinbutton'); fireEvent.change(input, { target: { value: '7' } }); fireEvent.submit(input.closest('form')!);
  await screen.findByText('Відхилено сервером.'); expect(input.getAttribute('aria-invalid')).toBe('true');
});
it('uses just the composed endpoint and explicitly shows partial personal data', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue({ movie: { id: 'movie', title: 'Кіно', releaseYear: 2026, posterUrl: null }, partial: true, myState: null, communityStatus: 'unavailable' });
  render(<MemoryRouter initialEntries={['/movies/movie']}><Routes><Route path="/movies/:id" element={<ClientDetails />} /></Routes></MemoryRouter>);
  await screen.findByText('Особисті дані тимчасово недоступні'); expect(request).toHaveBeenCalledTimes(1); expect(request.mock.calls[0][0]).toBe('/api/v1/screens/movies/movie');
});

it('does not reset a fast pagination click with the initial search debounce', async () => {
  vi.spyOn(api, 'request').mockImplementation(async path => {
    const page = Number(new URL('http://test' + path).searchParams.get('page'));
    return { items: [{ id: String(page), title: 'Page ' + page, releaseYear: 2026, posterUrl: null }], page, pageSize: 12, totalCount: 13 } as never;
  });
  render(<MemoryRouter><ClientCatalog /></MemoryRouter>);
  await screen.findByText('Page 1'); fireEvent.click(screen.getByRole('button', { name: 'Далі' }));
  await screen.findByText('Page 2');
  await new Promise(resolve => setTimeout(resolve, 450));
  expect(screen.getByText('Сторінка 2 з 2')).toBeTruthy();
});

it('shows a cached public catalog with an explicit stale warning after network failure', async () => {
  localStorage.clear();
  const data = { items: [{ id: 'cached', title: 'Cached film', releaseYear: 2026, posterUrl: null }], page: 1, pageSize: 12, totalCount: 1 };
  const request = vi.spyOn(api, 'request').mockResolvedValue(data);
  const view = render(<MemoryRouter><ClientCatalog /></MemoryRouter>);
  await screen.findByText('Cached film'); view.unmount();
  request.mockRejectedValue(new ApiError(0, 'Offline'));
  render(<MemoryRouter><ClientCatalog /></MemoryRouter>);
  await screen.findByText('Cached film');
  expect(screen.getByText(/Показано застарілі дані каталогу/)).toBeTruthy();
  localStorage.clear();
});
