export const ROLES = ["EQUITY_OFFICER", "APPROVER", "SYSTEM_ADMIN", "INQUIRY_USER"] as const;
export type Role = (typeof ROLES)[number];

export const TRANSACTION_STATUSES = ["PENDING_VERIFICATION", "VERIFIED", "REJECTED"] as const;
export type TransactionStatus = (typeof TRANSACTION_STATUSES)[number];

export const CERTIFICATE_REQUEST_STATUSES = ["PENDING_APPROVAL", "APPROVED", "REJECTED"] as const;
export type CertificateRequestStatus = (typeof CERTIFICATE_REQUEST_STATUSES)[number];

export const CERTIFICATE_STATUSES = ["ISSUED", "CANCELLED", "REPLACED", "REISSUED"] as const;
export type CertificateStatus = (typeof CERTIFICATE_STATUSES)[number];

export const PRINT_JOB_STATUSES = ["REQUESTED", "APPROVED", "REJECTED", "PRINTED"] as const;
export type PrintJobStatus = (typeof PRINT_JOB_STATUSES)[number];
