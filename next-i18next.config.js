const path = require("path");

module.exports = {
  i18n: {
    defaultLocale: "en",
    locales: ["en", "ru", "ar"],
    localeDetection: true,
  },
  localePath: path.resolve("./public/locales"),
  ns: ["common", "menu", "checkout", "admin"],
  defaultNS: "common",
};
