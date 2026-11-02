import { RefObject, useState } from 'react';

type PasswordFieldProps = {
  error?: string;
  inputRef?: RefObject<HTMLInputElement>;
  label: string;
  onChange: (value: string) => void;
  placeholder: string;
  value: string;
};

export function PasswordField({ error, inputRef, label, onChange, placeholder, value }: PasswordFieldProps) {
  const [isVisible, setIsVisible] = useState(false);

  return (
    <label className="password-field">
      {label}
      <span className="password-input-wrap">
        <input
          ref={inputRef}
          className={error ? 'input-error' : ''}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          placeholder={placeholder}
          type={isVisible ? 'text' : 'password'}
          aria-invalid={error ? 'true' : undefined}
        />
        <button
          type="button"
          onClick={() => setIsVisible((current) => !current)}
          aria-label={isVisible ? 'Приховати пароль' : 'Показати пароль'}
        >
          <EyeIcon isOff={isVisible} />
        </button>
      </span>
      {error ? <span className="field-error">{error}</span> : null}
    </label>
  );
}

function EyeIcon({ isOff }: { isOff: boolean }) {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" focusable="false">
      <path d="M2.4 12s3.6-6 9.6-6 9.6 6 9.6 6-3.6 6-9.6 6-9.6-6-9.6-6Z" />
      <circle cx="12" cy="12" r="3" />
      {isOff ? <path d="M4 4l16 16" /> : null}
    </svg>
  );
}
