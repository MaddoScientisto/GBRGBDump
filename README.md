The code for this libray has been ported from HerrZatacke's 
[gb-printer-web](https://github.com/HerrZatacke/gb-printer-web)

## Static web app

The solution now includes `GBRGBDump.Web.Static`, a .NET 10 standalone Blazor WebAssembly app that can be published as a static site for GitHub Pages.

It imports supported Game Boy Camera files in the browser, shows a simple preview grid, and exports either:

- one combined JSON payload, or
- a zip containing one file per imported photo in the selected non-JSON format.

The GitHub Actions workflow `.github/workflows/publish-github-pages.yml` publishes the app to the `gh-pages` branch for the GitHub Pages "Deploy from a branch" model.