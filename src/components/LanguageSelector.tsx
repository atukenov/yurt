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
    if (lang === "en" || lang === "ru") {
      setLanguage(lang);
    }
  };

  const languages = [
    { code: "en", name: "English", flag: "🇺🇸" },
    { code: "ru", name: "Русский", flag: "🇷🇺" },
  ];

  return (
    <div className="language-selector">
      <label htmlFor="language-select" className="sr-only">
        Select Language
      </label>
      {mounted && (
        <select
          id="language-select"
          value={language}
          onChange={(e) => handleLanguageChange(e.target.value)}
          className="language-dropdown"
        >
          {languages.map((lang) => (
            <option key={lang.code} value={lang.code}>
              {lang.flag} {lang.name}
            </option>
          ))}
        </select>
      )}
      <style jsx>{`
        .language-selector {
          display: inline-block;
        }

        .language-dropdown {
          padding: 0.5rem 1rem;
          border: 1px solid #ddd;
          border-radius: 4px;
          background-color: white;
          cursor: pointer;
          font-size: 1rem;
          transition: border-color 0.2s;
        }

        .language-dropdown:hover {
          border-color: #999;
        }

        .language-dropdown:focus {
          outline: none;
          border-color: #8b4513;
          box-shadow: 0 0 0 3px rgba(139, 69, 19, 0.1);
        }

        .sr-only {
          position: absolute;
          width: 1px;
          height: 1px;
          padding: 0;
          margin: -1px;
          overflow: hidden;
          clip: rect(0, 0, 0, 0);
          white-space: nowrap;
          border-width: 0;
        }
      `}</style>
    </div>
  );
};

export default LanguageSelector;
export { LanguageSelector };
