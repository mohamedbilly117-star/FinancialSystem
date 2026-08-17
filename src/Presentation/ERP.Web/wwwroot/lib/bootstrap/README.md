# wwwroot/lib/bootstrap — action required before first run

`Components/App.razor` references:

```
lib/bootstrap/css/bootstrap.rtl.min.css
```

Per the approved deployment model (Prompt 0: *Offline, LAN, No Cloud
Dependency*), Bootstrap 5 must be **vendored locally** in this folder
rather than loaded from a CDN, so the application works with no internet
access on the government LAN.

This documentation/scaffolding environment has no network access, so the
actual Bootstrap 5 distribution files could not be downloaded and placed
here. Before the first real build/run, copy the following into this folder
(from the official Bootstrap 5 release, or via `libman`/`npm` on a
connected development machine):

```
wwwroot/lib/bootstrap/css/bootstrap.rtl.min.css
wwwroot/lib/bootstrap/css/bootstrap.rtl.min.css.map   (optional, for debugging)
wwwroot/lib/bootstrap/js/bootstrap.bundle.min.js      (only if Bootstrap's JS components - modals, dropdowns - end up used alongside MudBlazor's own JS interop; many teams keep only the CSS and let MudBlazor own all interactive behavior)
```

Bootstrap ships an official `bootstrap.rtl.min.css` build specifically for
right-to-left layouts (matching the approved "Arabic First / RTL Native"
requirement, Prompt 8) - use that file, not the standard LTR
`bootstrap.min.css`.
