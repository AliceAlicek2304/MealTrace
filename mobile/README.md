# MealTrace for Android

Android-only Flutter companion app for the MealTrace ASP.NET Core API.

## Included flows

- Sign in with the same phone number/email and password as the web app.
- Parent: view linked children, create/edit/cancel meal absence periods.
- Teacher/Admin: review meal decisions and record a reasoned exception before the meal cutoff.
- Kitchen staff and other meal staff: view expected portions and immutable settled snapshots by class.
- API authorization remains enforced by the server; the app only shows role-appropriate navigation.

## Run

From this directory:

```sh
flutter pub get
flutter run
```

Enter the API origin in **Cấu hình kết nối API** on the sign-in screen. Android emulator can reach a host machine through `http://10.0.2.2:<port>`; a physical phone needs the host's LAN address. Use the API's HTTPS origin outside local development. Configure the API to listen on an address reachable from the device.

The access token is kept in memory for this initial mobile build and cleared at sign out. A cold app restart requires signing in again.
