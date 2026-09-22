// Тестовый помощник: JWT, который `shared/api/tokenClaims.decodeAccessToken`
// умеет разобрать. Подпись — заглушка, панель её не проверяет, и тестам это
// не нужно.

function base64url(value: object): string {
  const bytes = new TextEncoder().encode(JSON.stringify(value));
  let binary = '';
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** Кодирует произвольный payload в токен вида `header.payload.signature`. */
export function encodeTestToken(payload: Record<string, unknown>): string {
  return `${base64url({ alg: 'none', typ: 'JWT' })}.${base64url(payload)}.signature`;
}
