export function generateTransactionNo(): string {
  const ts = Date.now().toString(36).toUpperCase();
  const rand = Math.floor(Math.random() * 1000).toString().padStart(3, "0");
  return `TXN-${ts}-${rand}`;
}

export function generateCertificateNo(): string {
  const ts = Date.now().toString(36).toUpperCase();
  const rand = Math.floor(Math.random() * 1000).toString().padStart(3, "0");
  return `CERT-${ts}-${rand}`;
}
