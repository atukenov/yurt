import { errorMessage } from './http-error.interceptor';

describe('errorMessage', () => {
  it('answers in the language of the page', () => {
    expect(errorMessage('offline', 'ru')).toContain('интернета');
    expect(errorMessage('forbidden', 'kk')).toContain('рұқсатыңыз');
    expect(errorMessage('server', 'en')).toContain('went wrong');
  });

  it('falls back to English for an unknown language', () => {
    expect(errorMessage('offline', 'de')).toBe(errorMessage('offline', 'en'));
  });
});
