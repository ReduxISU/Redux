# Running Redux in production

Running redux on the production server is documented here.

## Initial configuration

We're going to assume the system is using systemd (the current
system process manager used by almost all Linux distributions).

First, install the systemd service file. The file below should
be installed to `/etc/systemd/system/redux.service`, owned by root/root and
not writeable by anyone else.

```
[Unit]
Description=redux
Documentation=Redux backend
After=network-online.target
After=remote-fs.target
Wants=network-online.target
Wants=remote-fs.target
ConditionPathExists=/home/USER/Redux

[Service]
Type=simple
User=USER
ExecStart=/home/USER/.dotnet/dotnet run
WorkingDirectory=/home/USER/Redux
ExecReload=/bin/kill $MAINPID
Restart=on-failure

[Install]
WantedBy=multi-user.target

#
# There are a few variables you might want to edit:
# - ExecStart
# - WorkingDirectory
# - User
# - ConditionPathExists
#
# ExecStart should be the location of the dotnet command
# which can be found by doing "which dotnet". It should be
# followed by the "run" command
#
# WorkingDirectory is the location of the API.csproj file
# for Redux (the base directory for the repository)
#
# User is the username of the user whose permissions should be
# used to run the backend.
#
# ConditionPathExists should be the same as WorkingDirectory
# (if you delete the directory, this service will not try to start)
```

To ensure the file ownership and mode:

```
chown root:root /etc/systemd/system/redux.service
chmod 644 /etc/systemd/system/redux.service
```

Make sure you kill off any currently running Redux server, and then
let's get systemd to do the rest of the work:

```
# systemd will notice/read the new service file
systemctl daemon-reload
# systemd will start the service on boot
systemctl enable redux.service
# go ahead and start it now
systemctl start redux.service
```

That's it to get things into normal operation!

## What about updates?

From time to time, you'll want to update the code base and
restart the service.

```
# get to the right directory
cd [working directory with API.csproj in it]

# pull changes into working directory
git pull origin

# restart the service
sudo systemctl restart redux.service
```

## What about errors?

Errors logged via `Console.WriteLine` are sent to the standard output
of dotnet programs. Luckily, systemd logs these in a circular buffer
by default. To view it:

```
# show the console log for redux
journalctl -xeu redux
```

## Stop the service

If you want to stop the service, easy enough:

```
sudo systemctl stop redux.service
```

Stopping it stops it for now. It will still restart on boot. To
disable it (make sure it never starts again):

```
sudo systemctl disable redux.service
```

## Why use systemd?

Running services as root is dangerous. If there's a vulnerability, the
attacker can run code as the user running the process. Using an
unpriviledged user means that the potential damage from a vulnerability
is limited drastically.

Further, we get logging infrastructure for free: systemd logs the
standard output in such a way that we don't have to worry about filling
the disk on really bad looping errors, etc.
