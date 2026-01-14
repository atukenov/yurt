import path from "path";

const config = {
  i18n: {
    defaultLocale: "ru",
    locales: ["en", "ru"],
    localeDetection: true,
  },
  localePath: path.resolve("./public/locales"),
  ns: ["common", "menu", "checkout", "admin"],
  defaultNS: "common",
};

export default config;
