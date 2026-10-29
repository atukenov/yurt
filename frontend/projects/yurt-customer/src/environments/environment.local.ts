// Used by `ng serve --configuration local` (scripts/dev.sh). Points at the local API container on the same computer
// the page came from, so a phone on the same Wi-Fi (`./scripts/dev.sh phone`) reaches the API too.
export const environment = {
  production: false,
  apiUrl: `http://${typeof location === 'undefined' ? 'localhost' : location.hostname}:8080`,
};
