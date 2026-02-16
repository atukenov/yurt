"use client";

import { useLanguage } from "@/context/LanguageContext";
import { useEffect, useState } from "react";

const LanguageSelector = () => {
  const { language, setLanguage } = useLanguage();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    const savedLanguage = localStorage.getItem("language") || "ru";
    setMounted(true);
  }, []);

  const handleLanguageChange = (lang: string) => {
    if (lang === "en" || lang === "ru" || lang === "kk") {
      setLanguage(lang);
    }
  };

  const languages = [
    { code: "en", name: "English", flag: "🇺🇸" },
    { code: "ru", name: "Русский", flag: "🇷🇺" },
    { code: "kk", name: "Қазақша", flag: "🇰🇿" },
  ];

  return (
    <div className="inline-block">
      <label htmlFor="language-select" className="sr-only">
        Select Language
      </label>
      {mounted && (
        <select
          id="language-select"
          value={language}
          onChange={(e) => handleLanguageChange(e.target.value)}
          className="px-4 py-2 border border-gray-300 rounded bg-white cursor-pointer text-base transition-colors duration-200 hover:border-gray-400 focus:outline-none focus:border-[#8b4513] focus:ring-3 focus:ring-[rgba(139,69,19,0.1)]"
        >
          {languages.map((lang) => (
            <option key={lang.code} value={lang.code}>
              {lang.flag} {lang.name}
            </option>
          ))}
        </select>
      )}
    </div>
  );
};

export default LanguageSelector;
export { LanguageSelector };
