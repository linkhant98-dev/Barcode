import { useEffect, useState } from "react";
import { api, apiErrorMessage } from "../api/client";
import { Certificate, PrintJob } from "../types";
import StatusBadge from "../components/StatusBadge";
import { useAuth } from "../context/AuthContext";

interface Preview {
  certificateNo: string;
  shareholderName: string;
  shareQuantity: number;
  issueDate: string;
  barcodeDataUrl: string;
}

export default function CertificatePrinting() {
  const { user } = useAuth();
  const [jobs, setJobs] = useState<PrintJob[]>([]);
  const [issuedCerts, setIssuedCerts] = useState<Certificate[]>([]);
  const [selectedCert, setSelectedCert] = useState("");
  const [preview, setPreview] = useState<Preview | null>(null);
  const [previewJobId, setPreviewJobId] = useState<number | null>(null);
  const [previewAlreadyPrinted, setPreviewAlreadyPrinted] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const [jobsRes, certsRes] = await Promise.all([
      api.get<PrintJob[]>("/print-jobs"),
      api.get<Certificate[]>("/certificates"),
    ]);
    setJobs(jobsRes.data);
    const requestedCertIds = new Set(
      jobsRes.data.filter((j) => j.status !== "REJECTED").map((j) => j.certificateId)
    );
    setIssuedCerts(certsRes.data.filter((c) => c.status === "ISSUED" && !requestedCertIds.has(c.id)));
  }

  useEffect(() => {
    load();
  }, []);

  async function requestPrint() {
    if (!selectedCert) return;
    setError(null);
    try {
      await api.post("/print-jobs", { certificateId: Number(selectedCert) });
      setSelectedCert("");
      await load();
    } catch (err) {
      setError(apiErrorMessage(err));
    }
  }

  async function decide(id: number, approve: boolean) {
    setError(null);
    try {
      await api.post(`/print-jobs/${id}/decision`, { approve });
      await load();
    } catch (err) {
      setError(apiErrorMessage(err));
    }
  }

  async function showPreview(id: number) {
    setError(null);
    try {
      const { data } = await api.get<Preview>(`/print-jobs/${id}/preview`);
      setPreview(data);
      setPreviewJobId(id);
      setPreviewAlreadyPrinted(jobs.find((j) => j.id === id)?.status === "PRINTED");
    } catch (err) {
      setError(apiErrorMessage(err));
    }
  }

  async function printCertificate() {
    if (!previewJobId) return;
    setError(null);
    try {
      await api.post(`/print-jobs/${previewJobId}/print`);
      setPreview(null);
      setPreviewJobId(null);
      await load();
    } catch (err) {
      setError(apiErrorMessage(err));
    }
  }

  const canRequest = user?.role === "EQUITY_OFFICER" || user?.role === "SYSTEM_ADMIN";
  const canApprove = user?.role === "APPROVER" || user?.role === "SYSTEM_ADMIN";

  return (
    <div>
      <h1 className="page-title">4. Certificate Printing</h1>
      <p className="page-subtitle">Request printing, get approval, preview the barcoded certificate, then print.</p>

      {error && <div className="error-banner">{error}</div>}

      {canRequest && (
        <div className="card">
          <h2>Request Certificate Printing</h2>
          {issuedCerts.length === 0 ? (
            <div className="empty-state">No issued certificates awaiting a print request.</div>
          ) : (
            <>
              <label>Issued Certificate</label>
              <select value={selectedCert} onChange={(e) => setSelectedCert(e.target.value)}>
                <option value="">Select a certificate...</option>
                {issuedCerts.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.certificateNo} — {c.shareholder.fullName}
                  </option>
                ))}
              </select>
              <div className="form-actions">
                <button disabled={!selectedCert} onClick={requestPrint}>
                  Request Printing
                </button>
              </div>
            </>
          )}
        </div>
      )}

      <div className="card">
        <h2>Print Jobs</h2>
        {jobs.length === 0 ? (
          <div className="empty-state">No print jobs yet.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Certificate No.</th>
                <th>Shareholder</th>
                <th>Requested By</th>
                <th>Status</th>
                <th>Printed By</th>
                <th>Printed At</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {jobs.map((j) => (
                <tr key={j.id}>
                  <td>{j.certificate.certificateNo}</td>
                  <td>{j.certificate.shareholder.fullName}</td>
                  <td>{j.requestedBy.name}</td>
                  <td>
                    <StatusBadge status={j.status} />
                  </td>
                  <td>{j.printedBy?.name ?? "—"}</td>
                  <td>{j.printedAt ? new Date(j.printedAt).toLocaleString() : "—"}</td>
                  <td>
                    {canApprove && j.status === "REQUESTED" && (
                      <>
                        <button className="ghost" onClick={() => decide(j.id, true)}>
                          Approve
                        </button>
                        <button className="ghost" onClick={() => decide(j.id, false)}>
                          Reject
                        </button>
                      </>
                    )}
                    {(j.status === "APPROVED" || j.status === "PRINTED") && (
                      <button className="ghost" onClick={() => showPreview(j.id)}>
                        {j.status === "PRINTED" ? "View" : "Preview"}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {preview && (
        <div
          className="modal-backdrop"
          onClick={() => {
            setPreview(null);
            setPreviewJobId(null);
          }}
        >
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Certificate Preview</h2>
            <p>
              <strong>{preview.certificateNo}</strong>
              <br />
              {preview.shareholderName} · {preview.shareQuantity.toLocaleString()} shares
              <br />
              Issued {new Date(preview.issueDate).toLocaleDateString()}
            </p>
            <div className="barcode-preview">
              <img src={preview.barcodeDataUrl} alt={`Barcode for ${preview.certificateNo}`} />
            </div>
            <div className="form-actions">
              {canRequest && !previewAlreadyPrinted && (
                <button onClick={printCertificate}>Print Certificate</button>
              )}
              <button
                className="secondary"
                onClick={() => {
                  setPreview(null);
                  setPreviewJobId(null);
                }}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
