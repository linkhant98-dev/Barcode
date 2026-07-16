export interface Shareholder {
  id: number;
  fullName: string;
  nationalId?: string | null;
  email?: string | null;
  phone?: string | null;
  address?: string | null;
}

export interface ShareTransaction {
  id: number;
  transactionNo: string;
  shareholderId: number;
  shareholder: Shareholder;
  shareQuantity: number;
  pricePerShare: number;
  paymentInfo: string;
  submittedVia: string;
  status: "PENDING_VERIFICATION" | "VERIFIED" | "REJECTED";
  verifiedBy?: { id: number; name: string } | null;
  createdAt: string;
}

export interface CertificateRequest {
  id: number;
  transactionId: number;
  transaction: ShareTransaction;
  requestedBy: { id: number; name: string };
  approvedBy?: { id: number; name: string } | null;
  status: "PENDING_APPROVAL" | "APPROVED" | "REJECTED";
  rejectionNote?: string | null;
  createdAt: string;
  certificate?: Certificate | null;
}

export interface CertificateHistoryEntry {
  id: number;
  action: string;
  notes?: string | null;
  performedBy: { id: number; name: string };
  createdAt: string;
}

export interface Certificate {
  id: number;
  certificateNo: string;
  barcodeValue: string;
  shareholderId: number;
  shareholder: Shareholder;
  shareQuantity: number;
  issueDate: string;
  status: "ISSUED" | "CANCELLED" | "REPLACED" | "REISSUED";
  history?: CertificateHistoryEntry[];
  replacesCertificate?: Certificate | null;
  replacedByCertificate?: Certificate | null;
}

export interface PrintJob {
  id: number;
  certificateId: number;
  certificate: Certificate;
  requestedBy: { id: number; name: string };
  approvedBy?: { id: number; name: string } | null;
  printedBy?: { id: number; name: string } | null;
  status: "REQUESTED" | "APPROVED" | "REJECTED" | "PRINTED";
  printedAt?: string | null;
  rejectionNote?: string | null;
  createdAt: string;
}

export interface ActivityLogEntry {
  id: number;
  action: string;
  entityType: string;
  entityId?: number | null;
  details?: string | null;
  createdAt: string;
  user?: { id: number; name: string; role: string } | null;
}
