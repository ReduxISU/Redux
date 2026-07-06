# Running Redux in production

Running redux on the production server is documented here.

## Common operations

### How to restart the services

All of the services are run as a single docker composition and it is
started and stopped via systemd. Members of the `redux` group are granted
permission to start/stop/restart the service via sudo (see
[How it is configured](#how-it-is-configured) below), so no password is
required.

To start/restart:
```
sudo systemctl restart redux
```

To stop:
```
sudo systemctl stop redux
```

If you want to disable the service (or re-enable it), systemctl's
enable/disable commands can be used. Note that the sudo grant only
covers start/stop/restart; enable/disable require a password.

### Viewing the logs

systemd services use journalctl to manage their logs. To view, use the command below.
Note that this log has entries for all of the redux services.

```
sudo journalctl -xeu redux
```
### Updating

The docker containers are NOT upgraded regularly and must be manually upgraded on
the production machine (portneuf). They are regularly updated on `redux.thought.net`,
though.

To update the packages on the production server:

```
$ sudo docker compose pull
[+] Pulling 4/4
 ✔ redux-gui Pulled                                                        0.8s
 ✔ mcpredux Pulled                                                         0.8s
 ✔ redux Pulled                                                            0.9s
 ✔ quantumsolver Pulled                                                    0.8s
$ sudo systemctl restart redux
```

The restart completes without a password prompt because the sudoers rule
grants the `redux` group NOPASSWD access to manage the service.

## How it is configured

All of the redux services (Redux_GUI, Redux, mcpredux, quantumsolver) are
composed into a set of docker containers. Each container is built by github actions
and the composition file uses those containers. There is no source code on the
production server.

The compose file can be found in `/home/redux/docker-compose.yml`. It currently
exposes the following ports to the internet at large:
 - Redux (http on 27000)
 - Redux_GUI (http on 3000)
 - mcpredux (http on 27200)
 - quantumsolver (http on 27100)

 Internally, Redux_GUI and mcpredux connect to Redux on the docker private network.
 Likewise, Redux connects to quantumsolver on the docker private network. The public
 exposure of the other ports is a legacy behavior.

In front of the composed container is an nginx server which handles TLS and connects
to redux as a reverse proxy.

 ### service restarts (sudo)

 Members of the `redux` group are allowed to start/stop/restart the service
 without a password via the following line in /etc/sudoers (edit with
 `visudo`):

 ```
 %redux ALL=(root) NOPASSWD: /usr/bin/systemctl restart redux, \
                             /usr/bin/systemctl stop redux, \
                             /usr/bin/systemctl start redux
 ```

 This covers start/stop/restart only; enabling/disabling the unit is not
 granted and still prompts for a password. The command match is exact, so
 only these three invocations (no extra arguments) are permitted.

 ### docker container updates

 This requires the following line in /etc/sudoers:

 ```
 %redux ALL=(root) CWD=/home/redux NOPASSWD: /usr/bin/docker compose pull
 ```

### systemd

The service file can be found in `/etc/systemd/system/redux.service` and
looks something like:

```
[Unit]
Description=Redux composition
After=docker.service
Requires=docker.service

[Service]
Type=simple
User=redux
Group=redux
WorkingDirectory=/home/redux
ExecStart=docker compose up
ExecStop=docker compose down
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
```
