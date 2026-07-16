import { useEffect, useState } from "react";
import { api, apiErrorMessage } from "../api/client";
import { CertificateRequest, ShareTransaction } from "../types";
import StatusBadge from "../components/StatusBadge";
import { useAuth } from "../context/AuthContext";

export default function CertificateCreation() {
  const { user } = useAuth();
  const [requests, setRequests] = useState<CertificateRequest[]>([]);
  const [verifiedTx, setVerifiedTx] = useState<ShareTransaction[]>([]);
  const [selectedTx, setSelectedTx] = useState<string>("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function load() {
    const [reqRes, txRes] = await Promise.all([
      api.get<CertificateRequest[]>("/certificate-requests"),
      api.get<ShareTransaction[]>("/share-transactions"),
    ]);
    setRequests(reqRes.data);
    const alreadyRequested = new Set(reqRes.data.map((r) => r.transactionId));
    setVerifiedTx(txRes.data.filter((t) => t.status === "VERIFIED" && !alreadyRequested.has(t.id)));
  }

  useEffect(() => {
    load();
  }, []);

  async function createRequest() {
    if (!selectedTx) return;
    setError(null);
    setLoading(true);
    try {
      await api.post("/certificate-requests", { transactionId: Number(selectedTx) });
      setSelectedTx("");
      await load();
    } catch (err) {
      setError(apiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  async function decide(id: number, approve: boolean) {
    setError(null);
    try {
      await api.post(`/certificate-requests/${id}/decision`, { approve });
      await load();
    } catch (err) {
      setError(apiErrorMessage(err));
    }
  }

  const canCreate = user?.role === "EQUITY_OFFICER" || user?.role === "SYSTEM_ADMIN";
  const canApprove = user?.role === "APPROVER" || user?.role === "SYSTEM_ADMIN";

  return (
    <div>
      <h1 className="page-title">2. Certificate Creation</h1>
      <p className="page-subtitle">
        Create a shareholder certificate request from a verified purchase, then have it reviewed and approved.
      </p>

      {error && <div className="error-banner">{error}</div>}

      {canCreate && (
        <div className="card">
          <h2>Create Shareholder Certificate Request</h2>
          {verifiedTx.length === 0 ? (
            <div className="empty-state">No verified purchase requests awaiting a certificate request.</div>
          ) : (
            <>
              <label>Verified Transaction</label>
              <select value={selectedTx} onChange={(e) => setSelectedTx(e.target.value)}>
                <option value="">Select a transaction...</option>
                {verifiedTx.map((tx) => (
                  <option key={tx.id} value={tx.id}>
                    {tx.transactionNo} — {tx.shareholder.fullName} ({tx.shareQuantity.toLocaleString()} shares)
                  </option>
                ))}
              </select>
              <div className="form-actions">
                <button disabled={!selectedTx || loading} onClick={createRequest}>
                  Create Certificate Request
                </button>
              </div>
            </>
          )}
        </div>
      )}

      <div className="card">
        <h2>Certificate Requests</h2>
        {requests.length === 0 ? (
          <div className="empty-state">No certificate requests yet.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Transaction No.</th>
                <th>Shareholder</th>
                <th>Requested By</th>
                <th>Status</th>
                <th>Approved By</th>
                <th>Certificate No.</th>
                {canApprove && <th>Actions</th>}
              </tr>
            </thead>
            <tbody>
              {requests.map((r) => (
                <tr key={r.id}>
                  <td>{r.transaction.transactionNo}</td>
                  <td>{r.transaction.shareholder.fullName}</td>
                  <td>{r.requestedBy.name}</td>
                  <td>
                    <StatusBadge status={r.status} />
                  </td>
                  <td>{r.approvedBy?.name ?? "—"}</td>
                  <td>{r.certificate?.certificateNo ?? "—"}</td>
                  {canApprove && (
                    <td>
                      {r.status === "PENDING_APPROVAL" && (
                        <>
                          <button className="ghost" onClick={() => decide(r.id, true)}>
                            Approve
                          </button>
                          <button className="ghost" onClick={() => decide(r.id, false)}>
                            Reject
                          </button>
                        </>
                      )}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
