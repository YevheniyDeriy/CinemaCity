# Происхождение извлечённого кода

Сервисы выделены из текущей рабочей копии CinemaCity, а не созданы по примеру магазина/бронирования. UUID, поля DTO и доменная терминология сохранены. SHA-256 ниже фиксирует исходник до выделения; последующие изменения добавляют границы сервисов, валидацию и требования лабораторных.

| Исходный файл монолита | Файл сервиса | SHA-256 исходника |
|---|---|---|
| `backend/CinemaCity.Application/Persistence/IRepository.cs` | `backend/CinemaCity.AuthService/Repositories/IRepository.cs` | `2d900669a74daf0c4388e920023206cd692fcc722a56f545cfc41545426ef887` |
| `backend/CinemaCity.Application/Persistence/IUnitOfWork.cs` | `backend/CinemaCity.AuthService/Repositories/IUnitOfWork.cs` | `28098ab83fda79f43312ed6c25ee86503dfed6dd03c90c190cbd605819950e11` |
| `backend/CinemaCity.Application/Persistence/IAppTransaction.cs` | `backend/CinemaCity.AuthService/Repositories/IAppTransaction.cs` | `4239f2ea92664c4985c66045392fee4d671c12e58292ec68fa061c90b66765b9` |
| `backend/CinemaCity.Application/Persistence/IDatabaseManager.cs` | `backend/CinemaCity.AuthService/Repositories/IDatabaseManager.cs` | `045d02489f82a7cb87fd1592322e24869cd25d256cf3c68191d7b7b26741e96c` |
| `backend/CinemaCity.Infrastructure/Persistence/EfRepository.cs` | `backend/CinemaCity.AuthService/Repositories/EfRepository.cs` | `4929abd6f49b7384682cb6d6af3263ea6a00536c6bfedbe65b203d1f3dd0aa21` |
| `backend/CinemaCity.Infrastructure/Persistence/UnitOfWork.cs` | `backend/CinemaCity.AuthService/Repositories/UnitOfWork.cs` | `2b08e60869b610aa5e65f1b322b3a154fbd8fa67ad3f4a48dab166c710917d0d` |
| `backend/CinemaCity.Infrastructure/Persistence/EfAppTransaction.cs` | `backend/CinemaCity.AuthService/Repositories/EfAppTransaction.cs` | `6b43062f6dba2b36ad1e7bf169f9d7f0e8609457126ca5762780404aaadd6c8c` |
| `backend/CinemaCity.Infrastructure/Persistence/DatabaseManager.cs` | `backend/CinemaCity.AuthService/Repositories/DatabaseManager.cs` | `b9805d16e18464264481a678b645cb0a9cf897be86e9370ed11f7150376c8c5e` |
| `backend/CinemaCity.Application/Persistence/IRepository.cs` | `backend/CinemaCity.CatalogService/Repositories/IRepository.cs` | `2d900669a74daf0c4388e920023206cd692fcc722a56f545cfc41545426ef887` |
| `backend/CinemaCity.Application/Persistence/IUnitOfWork.cs` | `backend/CinemaCity.CatalogService/Repositories/IUnitOfWork.cs` | `28098ab83fda79f43312ed6c25ee86503dfed6dd03c90c190cbd605819950e11` |
| `backend/CinemaCity.Application/Persistence/IAppTransaction.cs` | `backend/CinemaCity.CatalogService/Repositories/IAppTransaction.cs` | `4239f2ea92664c4985c66045392fee4d671c12e58292ec68fa061c90b66765b9` |
| `backend/CinemaCity.Application/Persistence/IDatabaseManager.cs` | `backend/CinemaCity.CatalogService/Repositories/IDatabaseManager.cs` | `045d02489f82a7cb87fd1592322e24869cd25d256cf3c68191d7b7b26741e96c` |
| `backend/CinemaCity.Infrastructure/Persistence/EfRepository.cs` | `backend/CinemaCity.CatalogService/Repositories/EfRepository.cs` | `4929abd6f49b7384682cb6d6af3263ea6a00536c6bfedbe65b203d1f3dd0aa21` |
| `backend/CinemaCity.Infrastructure/Persistence/UnitOfWork.cs` | `backend/CinemaCity.CatalogService/Repositories/UnitOfWork.cs` | `2b08e60869b610aa5e65f1b322b3a154fbd8fa67ad3f4a48dab166c710917d0d` |
| `backend/CinemaCity.Infrastructure/Persistence/EfAppTransaction.cs` | `backend/CinemaCity.CatalogService/Repositories/EfAppTransaction.cs` | `6b43062f6dba2b36ad1e7bf169f9d7f0e8609457126ca5762780404aaadd6c8c` |
| `backend/CinemaCity.Infrastructure/Persistence/DatabaseManager.cs` | `backend/CinemaCity.CatalogService/Repositories/DatabaseManager.cs` | `b9805d16e18464264481a678b645cb0a9cf897be86e9370ed11f7150376c8c5e` |
| `backend/CinemaCity.Domain/Entities/Movie.cs` | `backend/CinemaCity.CatalogService/Models/Movie.cs` | `7dfc0aaa97a536b42f33de8d82ec3127979a0190b0a602faee37e3db5ff3b768` |
| `backend/CinemaCity.Domain/Entities/Genre.cs` | `backend/CinemaCity.CatalogService/Models/Genre.cs` | `1951a619fd3e773067feded62408a314e3c42458fb5580f406bb96d6af0e6daf` |
| `backend/CinemaCity.Domain/Entities/Country.cs` | `backend/CinemaCity.CatalogService/Models/Country.cs` | `965c8b841eb177af101570bacc97de218f3f758b2d0da64f56f12f157bd5263b` |
| `backend/CinemaCity.Domain/Entities/Language.cs` | `backend/CinemaCity.CatalogService/Models/Language.cs` | `4d53b7402db691e05da8024d818c845ce4519c36d8f6343bc8d3823e61cc54ab` |
| `backend/CinemaCity.Domain/Entities/Person.cs` | `backend/CinemaCity.CatalogService/Models/Person.cs` | `518aac6a117d2e07f47ed147c6a356313480bb27f86e6f2a59971cff02361eff` |
| `backend/CinemaCity.Domain/Entities/MovieCast.cs` | `backend/CinemaCity.CatalogService/Models/MovieCast.cs` | `628d84b549dc4a0fa7f5f047ba1b6bd4ed97a5fc343960f5aed918778af54abc` |
| `backend/CinemaCity.Domain/Entities/Season.cs` | `backend/CinemaCity.CatalogService/Models/Season.cs` | `9d5169ef6623855856b81d94a8dd0538dfb51b01c51290073f7817bb99ae9472` |
| `backend/CinemaCity.Domain/Entities/Episode.cs` | `backend/CinemaCity.CatalogService/Models/Episode.cs` | `a3d85d422bb239bb641473437bfc8534f9ead868a26da58a402d2b9599e7c2a6` |
| `backend/CinemaCity.Domain/Enums/ContentType.cs` | `backend/CinemaCity.CatalogService/Models/ContentType.cs` | `3dcb3794ed4771db4185d6fd39afd5c675b325cd9baa0debf8593121f0208f89` |
| `backend/CinemaCity.Domain/Enums/CreditType.cs` | `backend/CinemaCity.CatalogService/Models/CreditType.cs` | `1e66c79de4a202ced78c59836bd02058a5c6d7a36e943b64ccaf51479d257fcd` |
| `backend/CinemaCity.Domain/Entities/User.cs` | `backend/CinemaCity.AuthService/Models/User.cs` | `fa02f9b19861b49e81b83a1b71c461f58d18576d2e2fd70f3db4faaf3323f636` |
| `backend/CinemaCity.Domain/Entities/RefreshToken.cs` | `backend/CinemaCity.AuthService/Models/RefreshToken.cs` | `06179984c1a830de88785393b802279c21cb5da8f7e4e8fee619ada5bf1501b7` |
| `backend/CinemaCity.Domain/Enums/UserRole.cs` | `backend/CinemaCity.AuthService/Models/UserRole.cs` | `10c8466db532c50bf87971d50f4137a0ba74fde0d80c0157601501f951bcfacf` |
| `backend/CinemaCity.Application/Persistence/IUserRepository.cs` | `backend/CinemaCity.AuthService/Repositories/IUserRepository.cs` | `5f81db3c3a6fc78664c9300a77d467efe4932741871d80423b12e230f0104c51` |
| `backend/CinemaCity.Application/Persistence/IRefreshTokenRepository.cs` | `backend/CinemaCity.AuthService/Repositories/IRefreshTokenRepository.cs` | `19f6f36bd49d88e53690946dda3627e0af5c7714075e899dbdbbc8f45afae7a4` |
| `backend/CinemaCity.Infrastructure/Persistence/UserRepository.cs` | `backend/CinemaCity.AuthService/Repositories/UserRepository.cs` | `024e772f84ba7caf357e66c49d766fa12f27dcbacec07ad7711fe067b1646439` |
| `backend/CinemaCity.Infrastructure/Persistence/RefreshTokenRepository.cs` | `backend/CinemaCity.AuthService/Repositories/RefreshTokenRepository.cs` | `80991da65c9969ebd8f1dac361f6057c4c1041a865688ea5e804f9d37cb07016` |
| `backend/CinemaCity.API/Auth/AuthEndpoints.cs` | `backend/CinemaCity.AuthService/Services/AuthEndpoints.cs` | `ee5fb013fa56fe033db3655c83e89ec2c4438f7a246855a1a4513231b7869527` |
| `backend/CinemaCity.API/Auth/TokenService.cs` | `backend/CinemaCity.AuthService/Services/TokenService.cs` | `4e5dc0aec569af4ec4b13dc96b0dca602561242dc1966c0b8fb1f527bc6d2254` |
| `backend/CinemaCity.API/Auth/JwtOptions.cs` | `backend/CinemaCity.AuthService/Services/JwtOptions.cs` | `8b41e8d37f646c22e33a82eae3e41609b50232ed15f695bb2e8c970e4bcc432e` |
| `backend/CinemaCity.API/Auth/AuthContracts.cs` | `backend/CinemaCity.AuthService/Dtos/AuthContracts.cs` | `fef20aa570ff39e8723cad93e6536be57ff07e88e07210442980488186022b36` |
| `backend/CinemaCity.API/Movies/MovieEndpoints.cs` | `backend/CinemaCity.CatalogService/Services/MovieEndpoints.cs` | `5e3666bc44440982762a9d66d570d1c912659b55e481c4974bdfd91d533915cb` |
| `backend/CinemaCity.API/Series/SeriesEndpoints.cs` | `backend/CinemaCity.CatalogService/Services/SeriesEndpoints.cs` | `dc1eabd36dd664f121137959357f77ea19f2c4699eb02735f8f1e001ef164f02` |
| `backend/CinemaCity.API/Admin/AdminEndpoints.cs` | `backend/CinemaCity.CatalogService/Services/AdminEndpoints.cs` | `4f8257595d6769a4dbe813c3837403fe5cc8b756503b05008830a4c47eb8c019` |
| `backend/CinemaCity.API/Admin/AdminImportEndpoints.cs` | `backend/CinemaCity.CatalogService/Services/AdminEndpoints.cs` | `a9aa42b6710ba0adf417a77f3faf685493414b235fee7af83b809c52bca87a8f` |
| `backend/CinemaCity.API/Admin/AdminEndpoints.cs` | `backend/CinemaCity.CatalogService/Services/AdminEndpoints.cs` | `4f8257595d6769a4dbe813c3837403fe5cc8b756503b05008830a4c47eb8c019` |
| `backend/CinemaCity.API/Search/SearchEndpoints.cs` | `backend/CinemaCity.CatalogService/Services/SearchEndpoints.cs` | `27a950171bd674780edc27110d537e8af286138ec2b04beb99cb6898c50c105d` |
| `backend/CinemaCity.Application/Imports/ITmdbImportService.cs` | `backend/CinemaCity.CatalogService/Imports/ITmdbImportService.cs` | `c424f3985bdc1732eafafec7393f8115ec793f9db632fe9f5e6fcf1c4d32ddcb` |
| `backend/CinemaCity.Application/Imports/ImportResultDto.cs` | `backend/CinemaCity.CatalogService/Imports/ImportResultDto.cs` | `9c4439e8c2959f8713c011ba5f0386ecc44aac926d62e3fb665f62bae465c98e` |
| `backend/CinemaCity.Infrastructure/Tmdb/NullableDateOnlyJsonConverter.cs` | `backend/CinemaCity.CatalogService/Tmdb/NullableDateOnlyJsonConverter.cs` | `207cec3f8a9b4229cb3e77ca8d7071376adf7022464bedaf835cd668f7269587` |
| `backend/CinemaCity.Infrastructure/Tmdb/TmdbClient.cs` | `backend/CinemaCity.CatalogService/Tmdb/TmdbClient.cs` | `08d3b45143c67bff1be8c171aa3370a16d295774a0a2d34d0109793dd99d1305` |
| `backend/CinemaCity.Infrastructure/Tmdb/TmdbDtos.cs` | `backend/CinemaCity.CatalogService/Tmdb/TmdbDtos.cs` | `f4ca1cdefa852462a2cfbb6b0a5c9be7eceb1f7551610f30ebae70e97602ea23` |
| `backend/CinemaCity.Infrastructure/Tmdb/TmdbImportService.cs` | `backend/CinemaCity.CatalogService/Tmdb/TmdbImportService.cs` | `f4a4ca6c1b66e85d25046bd37fa4eecc21d181938f0e242b3be95a7e8ff78049` |
| `backend/CinemaCity.Infrastructure/Tmdb/TmdbOptions.cs` | `backend/CinemaCity.CatalogService/Tmdb/TmdbOptions.cs` | `c43ec5fff94e0b60973a22988b7aec6c9e13c35f3eee6a1c38f4a2d2f1488301` |
