# Deploying PharmaERP

Three apps, one SQL Server database:

| App | Project | What it is | Example address |
|---|---|---|---|
| API | `src/Web.Api` | ASP.NET Core; the field app talks only to this | `https://api.example.com` |
| Dashboard | `src/Web.Mvc` | ASP.NET Core; managers and admins | `https://admin.example.com` |
| Field app | `src/FieldApp` | Static files (Blazor WebAssembly PWA) | `https://field.example.com` |

**HTTPS is required for all three.** Phones only allow location, installing the app, push notifications and fingerprint sign-in on HTTPS.

The field app needs its own host name. Fingerprint sign-in (passkeys) is tied to that name: if it changes later, every rep has to set up fingerprint sign-in again.

## 1. Build the release

```powershell
./deploy/publish.ps1 -Output D:\Releases\2026-10-05 -FieldAppSettings D:\Config\fieldapp.appsettings.json
```

- The script builds in Release, runs every test, publishes `api\`, `dashboard\` and `fieldapp\`, and fails if any key file ended up in the output.
- Make `fieldapp.appsettings.json` from [settings/FieldApp.appsettings.json](settings/FieldApp.appsettings.json) and set `Api:BaseUrl`. Every phone downloads this file, so it must hold no secrets.
- The script puts this file in **before** publishing. Don't edit `appsettings.json` in a published field app afterwards. The service worker checks every file against the hash recorded at publish time, and a changed file stops the app from installing for offline use.

CI ([.github/workflows/ci.yml](../.github/workflows/ci.yml)) runs the same script on every push. It keeps the output as an artifact, but that copy points at the development API, so don't deploy it.

## 2. Secrets: environment variables or a vault, never in a file in git

| Setting | API | Dashboard | How to make it |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | ✓ | ✓ | SQL Server connection string |
| `Jwt__Key` | ✓ | | `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))` |
| `WebPush__PublicKey`, `WebPush__PrivateKey` | ✓ | ✓ (same pair) | See the note below |

- **API startup checks.** Without `Jwt__Key` the API refuses to start in Production. It also refuses the key that was once published in this repository.
- **Web Push keys.** If they are not set, the first app to start generates a pair into `vapid.json` in the key folder. Both apps then use that pair, because they share the folder. Either way, **never change the pair** once reps have subscribed: every phone would stop receiving notifications until the rep opens the app again.
- **Where to set them.**
  - IIS: set them on the application pool, so they live in `applicationHost.config` and survive a redeploy (never in the site's `web.config`). Run as administrator:

    ```cmd
    %windir%\system32\inetsrv\appcmd set config -section:system.applicationHost/applicationPools /+"[name='PharmaApi'].environmentVariables.[name='Jwt__Key',value='...']" /commit:apphost
    ```
  - systemd: an `EnvironmentFile=` readable only by the service user.

## 3. Settings files (no secrets)

Copy each template next to its app as `appsettings.Production.json`:

- [settings/Web.Api.appsettings.Production.json](settings/Web.Api.appsettings.Production.json)
- [settings/Web.Mvc.appsettings.Production.json](settings/Web.Mvc.appsettings.Production.json)

| Setting | Meaning |
|---|---|
| `Cors:FieldAppOrigins` | The field app's exact origin, `https://field.example.com`, with no trailing slash. Only this origin may call the API and complete fingerprint sign-in. |
| `Passkeys:ServerDomain` | The field app's host name, `field.example.com`. |
| `Storage:KeysPath`, `Storage:UploadsPath` | **The same two folders for the API and the dashboard**, outside the site folders so a redeploy can't delete them. |
| `ReverseProxy:KnownProxies` | IP addresses of a reverse proxy on another machine. Loopback is trusted by default, so leave this empty under IIS or with nginx on the same server. |
| `AllowedHosts` | The app's own host name. |

What the two storage folders hold:

- **Keys folder:** the Data Protection key ring, which keeps users signed in to the dashboard, and `vapid.json`. Back it up and restrict it to the two app identities. Losing it signs every dashboard user out, and if `vapid.json` was generated rather than configured, it also stops push notifications.
- **Uploads folder:** photos of receipts and collections. Back it up with the database.

Both app identities (IIS application pools or the systemd user) need **Modify** rights on both folders.

## 4. Database

- Both apps apply pending EF migrations when they start. The first start creates the schema.
- On every upgrade, **back up the database first**, then start the API alone once and check `/health` before starting the dashboard.
- The SQL login needs `db_owner` for the migrations. A later hardening step can split this into a migration login and a runtime login.

## 5. First sign-in

The dashboard creates `admin@pharmaerp.local` with the password `ChangeMe!2026` on first start. **That password is in this public repository.**

Sign in straight away, change it, and create named admin accounts. Do this before the dashboard is reachable from the internet.

## 6. Hosting

### Windows / IIS

1. Install the **.NET 10 Hosting Bundle** and the **IIS URL Rewrite** module.
2. Create the **API** and **Dashboard** sites from `api\` and `dashboard\`, each with its own application pool set to *No Managed Code*.
3. Create the **Field app** site from `fieldapp\`. Its application pool can also be *No Managed Code*. Its [web.config](../src/FieldApp/web.config):
   - serves the `wwwroot` folder;
   - sends the compressed `.br` files;
   - sets the security headers, including the CSP;
   - makes every file revalidate.

   In that `web.config`, narrow `connect-src https:` to the API's origin.
4. Add HTTPS bindings with real certificates.

### Linux / nginx

- [nginx/pharmaerp.conf](nginx/pharmaerp.conf) has all three sites. The API and the dashboard run under Kestrel on localhost only. A systemd unit for the API:

  ```ini
  [Unit]
  Description=PharmaERP API
  After=network.target

  [Service]
  WorkingDirectory=/var/www/pharmaerp/api
  ExecStart=/usr/bin/dotnet /var/www/pharmaerp/api/PharmaERP.Web.Api.dll
  Environment=ASPNETCORE_ENVIRONMENT=Production
  Environment=ASPNETCORE_URLS=http://127.0.0.1:5001
  EnvironmentFile=/etc/pharmaerp/api.env
  User=pharmaerp
  Restart=always

  [Install]
  WantedBy=multi-user.target
  ```

- The dashboard's unit is the same, with `PharmaERP.Web.Mvc.dll`, port `5002`, and its own env file.
- On Linux, use `/var/lib/pharmaerp/keys` and `/var/lib/pharmaerp/uploads` for the storage paths.

## 7. Check after every deployment

- [ ] `https://api.example.com/health` and `https://admin.example.com/health` return `Healthy`.
- [ ] Open the field app in a browser, then DevTools → Network:
  - `_framework/*.wasm` comes back with `content-encoding: br` and `cache-control: no-cache`;
  - the response headers include `content-security-policy`;
  - the console shows no CSP errors.
- [ ] Sign in on a real phone over mobile data (not office Wi-Fi). Then:
  - open today's plan;
  - check in to a visit;
  - turn on airplane mode, check out, turn it off, and confirm the visit syncs.
- [ ] Turn on notifications on that phone. Approve one of its expenses from the dashboard and confirm the notification arrives.
- [ ] Set up fingerprint sign-in on that phone, sign out, and sign back in with it.

## 8. Releasing an update

- Publish, then copy over the site folders. Leave the storage folders and the `appsettings.Production.json` files alone.
- Reps get an **Update** banner the next time they open the app. It never reloads by itself while an order is being entered.
- Anything waiting in the outbox survives the update.

## 9. Pilot week

- Dashboard → **Field Pilot**: GPS accuracy and fix time against the targets (≥ 90 % within 30 m, ≥ 90 % within 10 s), plus flagged visits and visits left open, for each rep. **CSV** exports the same table.
- Settings you can tune, all in the field app's settings (republish after changing them):
  - `Gps:TargetAccuracyMeters`;
  - `Gps:MaxAcceptedAccuracyMeters`;
  - `Gps:GeofenceRadiusDoctorMeters` and `Gps:GeofenceRadiusPharmacyMeters`;
  - the tracking interval.
- Read the report's "Reading it" notes before widening a geofence. Usually the stored customer location is wrong, or the rep checks in indoors, and a wider fence hides that rather than fixing it.
