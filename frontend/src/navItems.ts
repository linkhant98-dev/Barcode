import { Role } from "./context/AuthContext";

export const NAV_ITEMS: { to: string; label: string; roles: Role[] }[] = [
  { to: "/purchase", label: "1. Share Purchase", roles: ["EQUITY_OFFICER", "SYSTEM_ADMIN"] },
  { to: "/certificate-requests", label: "2. Certificate Creation", roles: ["EQUITY_OFFICER", "APPROVER", "SYSTEM_ADMIN"] },
  { to: "/certificates", label: "3. Certificate Management", roles: ["EQUITY_OFFICER", "APPROVER", "SYSTEM_ADMIN", "INQUIRY_USER"] },
  { to: "/printing", label: "4. Certificate Printing", roles: ["EQUITY_OFFICER", "APPROVER", "SYSTEM_ADMIN"] },
  { to: "/reports", label: "5. Reporting & Inquiry", roles: ["EQUITY_OFFICER", "APPROVER", "SYSTEM_ADMIN", "INQUIRY_USER"] },
];

/** First page in the workflow a given role is actually allowed to land on. */
export function getDefaultRoute(role: Role | undefined): string {
  return NAV_ITEMS.find((item) => !role || item.roles.includes(role))?.to ?? "/certificates";
}
