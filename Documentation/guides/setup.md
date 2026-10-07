# Setup: install, run, and check that it works

This guide gets Redux running on your own computer. You only do the install steps once. After that, starting Redux is a single command.

Some words you will see:

- **SDK** (software development kit): the toolbox .NET needs to build and run C# code. Redux uses **.NET 10**.
- **Git**: the program that tracks changes to code and lets you share them through GitHub.
- **Fork**: your own copy of the Redux repo on GitHub. You make changes in your copy, then ask the maintainers to take them (that request is a **pull request**, or PR).
- **Clone**: downloading a repo from GitHub to your computer.
- **API** (application programming interface): a program that other programs talk to over the web. Redux is an API. The website (Redux_GUI) asks it questions such as "what problems do you have?".
- **Swagger**: a web page, built into Redux, that lists every API call and lets you try them with a button.
- **Port**: a numbered "door" on your computer. Redux listens on door number 27000.
- **Docker**: a tool that runs a program inside a small, self-contained Linux box called a **container**, so it behaves the same on every computer.

---

## 1. Install what you need

| Tool | Why | Check that it works |
| --- | --- | --- |
| [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download) | Builds and runs Redux. | `dotnet --version` prints something starting with `10.` |
| [Git](https://git-scm.com/downloads) | Downloads the code and sends your changes back. | `git --version` prints a version number |
| An editor: [VS Code](https://code.visualstudio.com/) with the C# extension (`ms-dotnettools.csharp`), [Visual Studio](https://visualstudio.microsoft.com/), or [JetBrains Rider](https://www.jetbrains.com/rider/) | Where you write code. Any one of them is fine. | It opens `Redux.slnx` and shows no red errors |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) (optional) | Only for the Docker and dev container options below. | `docker --version` |
| [Node.js 26](https://nodejs.org/en/download) (optional) | Only if you will run the website (Redux_GUI). The API does not need it. | `node --version` |

Open a terminal (on Windows, Git Bash or PowerShell) and run:

```bash
dotnet --list-sdks
```

You should see a line starting with `10.` in the list. Having older SDKs installed too (6.0, 9.0) is fine. Redux targets .NET 10 (see `TargetFramework` in [API.csproj](../../API.csproj)), and .NET uses the newest SDK on your machine. If there is no `10.` line, install the .NET 10 SDK and open a new terminal. See also [troubleshooting.md](troubleshooting.md).

## 2. Get the code (fork and clone)

The workflow in [CONTRIBUTING.md](../../CONTRIBUTING.md) is: fork, make a branch in your fork, open a PR back to the main repo. The main repo's working branch is `CSharpAPI`. There is no `develop` branch.

1. On GitHub, open <https://github.com/ReduxISU/Redux> and click **Fork**.
2. Clone **your fork** (replace `YOUR-USERNAME`):

   ```bash
   git clone https://github.com/YOUR-USERNAME/Redux.git
   cd Redux
   ```

3. Add the main repo as a second remote named `upstream`, so you can pull in other people's new work:

   ```bash
   git remote add upstream https://github.com/ReduxISU/Redux.git
   git fetch upstream
   ```

   `git remote -v` should now list both `origin` (your fork) and `upstream` (ReduxISU).
4. Make a branch for your work, starting from the newest `CSharpAPI`:

   ```bash
   git switch -c my-change upstream/CSharpAPI
   ```

   Use a descriptive name instead of `my-change`, for example `add-reduction-subsetsum-to-partition`.

Later, to bring your branch up to date with the main repo, run `git fetch upstream` and then `git merge upstream/CSharpAPI`. The task guides say `origin/CSharpAPI` in their branch commands. If you work from a fork, read that as `upstream/CSharpAPI`.

Do not finish (merge) your own PR before someone has reviewed it.

## 3. Run the API

Run these from the repo root, the folder that contains `API.csproj` and `Redux.slnx`.

```bash
dotnet run
```

A bare `dotnet run` works, because it only looks for a project file (`API.csproj`) and ignores `Redux.slnx`. This is different from `dotnet build` and `dotnet test`, which fail with `error MSB1011` when you do not name a file (see [troubleshooting.md](troubleshooting.md)). If you prefer to be explicit, `dotnet run --project API.csproj` does the same thing.

The first run downloads packages and builds, so it can take a minute. Success looks like this:

```text
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:27000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

Your browser may open the Swagger page by itself. The port and the Swagger start page come from [Properties/launchSettings.json](../../Properties/launchSettings.json).

Now check that it works. Open these in a browser (or use `curl`):

| Address | What you should see |
| --- | --- |
| <http://127.0.0.1:27000/health> | The word `ok` |
| <http://127.0.0.1:27000/swagger/index.html> | The Swagger page listing the API calls |
| <http://127.0.0.1:27000/Navigation/Reductions> | A long block of JSON (every reduction, grouped by the problem it starts from) |

In Swagger you can open a call, click **Try it out**, then **Execute**. Calls have example values already filled in.

To stop the API, click in the terminal and press `Ctrl+C`.

## 4. Auto-reload while you edit: `dotnet watch`

`dotnet watch` runs the API and restarts it every time you save a file, so you do not have to stop and start it by hand.

```bash
dotnet watch --project API.csproj run
```

Success looks like the normal startup text, plus lines beginning with `dotnet watch` that say it is watching for changes. If it asks whether to restart after an edit it cannot apply live, press `y`. Press `Ctrl+C` to stop.

(An older doc used `dotnet watch --project API.csproj run -- --project API.csproj`. The extra `-- --project API.csproj` is not needed.)

## 5. Run it in Docker (optional)

This builds the same image that is deployed to the server. It is slower than `dotnet run`, so use it only to check the container itself. From the repo root, with Docker running:

```bash
docker build -t reduxapi .
docker run -it --rm -p 27000:27000 --name reduxapi reduxapi
```

`-p 27000:27000` means "connect door 27000 on my computer to door 27000 inside the container". The [Dockerfile](../../Dockerfile) sets `ASPNETCORE_HTTP_PORTS=27000` and `EXPOSE 27000`, so the app listens on 27000 inside the container. (Older docs said `-p 27000:80`. That is wrong for the current Dockerfile, and you would get "connection refused".)

Success: <http://127.0.0.1:27000/health> shows `ok`. Press `Ctrl+C` to stop. `--rm` deletes the container afterwards.

The image runs the published, release build, so some warnings look different from `dotnet run`.

## 6. Use the dev container (optional)

A **dev container** is a ready-made Linux workspace with the .NET 10 SDK and the Redux Build System (`rbs`) already installed. It is the closest match to what the automatic checks on GitHub use. The settings are in [.devcontainer/devcontainer.json](../../.devcontainer/devcontainer.json).

1. Install Docker Desktop and VS Code, and start Docker.
2. Open the repo folder in VS Code.
3. Press `F1`, type `Reopen in Container`, and pick it. The first time takes a few minutes.
4. Use the terminal inside VS Code. The commands in this guide work there too. VS Code offers to forward port 27000 so you can open Swagger in your normal browser.

What to do with `rbs` is explained in [building-and-testing.md](building-and-testing.md).

## 7. Run the website next to it (optional)

The website is a separate repo, [Redux_GUI](https://github.com/ReduxISU/Redux_GUI). It is one of the frontends for this API. You do not need it to work on the API, since Swagger and the tests are enough for most tasks. If you want to see your change on the website:

1. Keep the API running (section 3) in one terminal.
2. In a second terminal, clone and run Redux_GUI by following its README: <https://github.com/ReduxISU/Redux_GUI/blob/ReduxAPI_GUI/README.md>. It needs Node.js 26.
3. The website talks to the API on port 27000, so keep that port free for Redux.

## 8. If something goes wrong

- `dotnet` is not found, or the SDK is the wrong version: see [troubleshooting.md](troubleshooting.md).
- "address already in use" on port 27000: another copy of Redux is probably still running. See [troubleshooting.md](troubleshooting.md).

## Next

Next: [building-and-testing.md](building-and-testing.md), then pick a task guide from the [guides index](README.md). Reading [how-the-code-works.md](how-the-code-works.md) first will make the task guides easier to follow.
