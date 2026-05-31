# Running Redux on portneuf

Running redux on the production server is documented here.

## Introduction

`Redux` and `Redux_GUI` are both managed by systemd now. Which means
there is no running shell scripts + screen, etc. any more. The first
part of this document is structured as "how do I? questions.

### How do I restart the server components?

If you are in the "redux" group on portneuf, this is easy.

```
sudo systemctl restart redux.service
sudo systemctl restart reduxgui.service
```

### How do I delegate updating and restarting of redux/redux_gui?

Edit the file `/etc/group` and include (or delete) the user from
the `redux` group. You have to log out and log back in if you're
updating your own membership in this group.

### How do I update redux?

```bash
sudo -u redux bash --login
cd ~redux/Redux
git pull origin CSharpAPI
sudo systemctl restart redux.service
```

Note: there's no need to build as the restart script for systemd will
do that automatically.

### How do I update redux_gui?

```bash
sudo -u redux bash --login
cd ~redux/Redux_GUI
git pull origin ReduxAPI_GUI
sudo systemctl restart reduxgui.service
```

Note: there's no need to build as the restart script for systemd will
do that automatically.

### How do I check the status?

```bash
systemctl status redux
systemctl status reduxgui
```

### How do I view the logs?

```bash
journalctl -u redux
journalctl -u reduxgui
```

## System Configuration

Redux/Redux_GUI are run as reverse proxies with Nginx as the internet-facing
compondent. The backend services are run using systemd and are configured to
restart automatically on failure.

The configuration for nginx lives in `/etc/nginx` and and most specifically
all of the magic happens in `/etc/nginx/sites-enables/default`.