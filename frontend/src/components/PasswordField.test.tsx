import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { PasswordField } from './PasswordField';

describe('PasswordField', () => {
  it('toggles password visibility and forwards typed value', async () => {
    const user = userEvent.setup();
    const handleChange = vi.fn();

    render(
      <PasswordField
        label="Пароль"
        value="secret"
        onChange={handleChange}
        placeholder="Ваш пароль"
      />
    );

    const input = screen.getByLabelText('Пароль');
    expect(input).toHaveAttribute('type', 'password');

    await user.click(screen.getByRole('button', { name: 'Показати пароль' }));
    expect(input).toHaveAttribute('type', 'text');

    await user.click(screen.getByRole('button', { name: 'Приховати пароль' }));
    expect(input).toHaveAttribute('type', 'password');

    await user.type(input, '!');
    expect(handleChange).toHaveBeenLastCalledWith('secret!');
  });

  it('renders validation error state accessibly', () => {
    render(
      <PasswordField
        label="Пароль"
        value=""
        onChange={() => {}}
        placeholder="Мінімум 8 символів"
        error="Пароль має містити щонайменше 8 символів."
      />
    );

    expect(screen.getByPlaceholderText('Мінімум 8 символів')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Пароль має містити щонайменше 8 символів.')).toBeInTheDocument();
  });
});
