"use client";

import { InputHTMLAttributes } from "react";

interface FormFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  hint?: string;
  icon?: React.ReactNode;
  required?: boolean;
}

export function FormField({
  label,
  error,
  hint,
  icon,
  required,
  className = "",
  ...props
}: FormFieldProps) {
  return (
    <div className="mb-4">
      {label && (
        <label className="block text-sm font-semibold text-gray-900 mb-2">
          {label}
          {required && <span className="text-red-600 ml-1">*</span>}
        </label>
      )}

      <div className="relative">
        {icon && (
          <div className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400">
            {icon}
          </div>
        )}

        <input
          className={`
            w-full px-4 py-2 border rounded-lg transition
            ${icon ? "pl-10" : ""}
            ${
              error
                ? "border-red-500 bg-red-50 focus:ring-red-500 focus:border-red-500"
                : "border-gray-300 focus:ring-amber-500 focus:border-amber-600"
            }
            focus:outline-none focus:ring-2
            ${className}
          `}
          {...props}
        />
      </div>

      {error && (
        <p className="mt-1 text-sm text-red-600 flex items-center gap-1">
          <span>⚠️</span>
          {error}
        </p>
      )}

      {hint && !error && <p className="mt-1 text-xs text-gray-500">{hint}</p>}
    </div>
  );
}

interface TextAreaFieldProps extends InputHTMLAttributes<HTMLTextAreaElement> {
  label?: string;
  error?: string;
  hint?: string;
  required?: boolean;
  rows?: number;
}

export function TextAreaField({
  label,
  error,
  hint,
  required,
  rows = 4,
  className = "",
  ...props
}: TextAreaFieldProps) {
  return (
    <div className="mb-4">
      {label && (
        <label className="block text-sm font-semibold text-gray-900 mb-2">
          {label}
          {required && <span className="text-red-600 ml-1">*</span>}
        </label>
      )}

      <textarea
        rows={rows}
        className={`
          w-full px-4 py-2 border rounded-lg transition
          ${
            error
              ? "border-red-500 bg-red-50 focus:ring-red-500 focus:border-red-500"
              : "border-gray-300 focus:ring-amber-500 focus:border-amber-600"
          }
          focus:outline-none focus:ring-2 resize-vertical
          ${className}
        `}
        {...props}
      />

      {error && (
        <p className="mt-1 text-sm text-red-600 flex items-center gap-1">
          <span>⚠️</span>
          {error}
        </p>
      )}

      {hint && !error && <p className="mt-1 text-xs text-gray-500">{hint}</p>}
    </div>
  );
}

interface SelectFieldProps extends InputHTMLAttributes<HTMLSelectElement> {
  label?: string;
  error?: string;
  hint?: string;
  required?: boolean;
  options: Array<{ value: string; label: string }>;
}

export function SelectField({
  label,
  error,
  hint,
  required,
  options,
  className = "",
  ...props
}: SelectFieldProps) {
  return (
    <div className="mb-4">
      {label && (
        <label className="block text-sm font-semibold text-gray-900 mb-2">
          {label}
          {required && <span className="text-red-600 ml-1">*</span>}
        </label>
      )}

      <select
        className={`
          w-full px-4 py-2 border rounded-lg transition
          ${
            error
              ? "border-red-500 bg-red-50 focus:ring-red-500 focus:border-red-500"
              : "border-gray-300 focus:ring-amber-500 focus:border-amber-600"
          }
          focus:outline-none focus:ring-2
          ${className}
        `}
        {...props}
      >
        <option value="">Select an option</option>
        {options.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>

      {error && (
        <p className="mt-1 text-sm text-red-600 flex items-center gap-1">
          <span>⚠️</span>
          {error}
        </p>
      )}

      {hint && !error && <p className="mt-1 text-xs text-gray-500">{hint}</p>}
    </div>
  );
}

interface FormErrorProps {
  error?: string;
}

export function FormError({ error }: FormErrorProps) {
  if (!error) return null;
  return (
    <div className="p-4 bg-red-50 border border-red-200 rounded-lg mb-4">
      <p className="text-red-800 font-semibold flex items-center gap-2">
        <span>❌</span>
        {error}
      </p>
    </div>
  );
}

interface FormSuccessProps {
  message?: string;
}

export function FormSuccess({ message }: FormSuccessProps) {
  if (!message) return null;
  return (
    <div className="p-4 bg-green-50 border border-green-200 rounded-lg mb-4">
      <p className="text-green-800 font-semibold flex items-center gap-2">
        <span>✅</span>
        {message}
      </p>
    </div>
  );
}

interface FormFieldsErrorProps {
  errors?: Record<string, string>;
}

export function FormFieldsError({ errors }: FormFieldsErrorProps) {
  if (!errors || Object.keys(errors).length === 0) return null;

  return (
    <div className="p-4 bg-red-50 border border-red-200 rounded-lg mb-4">
      <p className="text-red-800 font-semibold mb-2">
        Please fix the following errors:
      </p>
      <ul className="space-y-1">
        {Object.entries(errors).map(([field, message]) => (
          <li
            key={field}
            className="text-sm text-red-700 flex items-start gap-2"
          >
            <span>•</span>
            <span>
              <strong>{field}:</strong> {message}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
