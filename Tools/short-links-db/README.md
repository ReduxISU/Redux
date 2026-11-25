# Short-link database (development)

The share endpoints (`POST /ShareLinks`, `GET /ShareLinks/{id}`, issue #844) keep their links in PostgreSQL. This folder
starts one on your machine with Docker, for development only. Without a database the share endpoints answer
HTTP 503 `short_links_unavailable` and everything else in the API works as usual, so you only need this when you work
on share links.

Start it (from this folder):

```bash
docker compose up -d
```

Then run the API from the repo root with the connection string in the environment:

```bash
# bash
ConnectionStrings__ShortLinks="Host=localhost;Port=5432;Database=redux;Username=redux;Password=redux-dev-only" dotnet run
```

```powershell
# PowerShell
$env:ConnectionStrings__ShortLinks = "Host=localhost;Port=5432;Database=redux;Username=redux;Password=redux-dev-only"
dotnet run
```

The API makes its table (`share_links`) on first use. The data stays in the `short-links-data` volume between runs.
Stop it with `docker compose down`, or delete the data too with `docker compose down -v`.

The password is for this local database only (it listens on 127.0.0.1 only). A real server sets its own connection
string in `ConnectionStrings__ShortLinks` and never commits it. The other settings (`ShortLinks:LifetimeDays`,
`MaxViewBytes`, `CreatesPerHourPerAddress`, `CleanupMinutes`) are explained in `appsettings.json`.
