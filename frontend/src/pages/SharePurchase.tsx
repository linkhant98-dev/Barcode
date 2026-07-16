import { FormEvent, useEffect, useState } from "react";
import { api, apiErrorMessage } from "../api/client";
import { ShareTransaction } from "../types";
import StatusBadge from "../components/StatusBadge";
import { useAuth } from "../context/AuthContext";

const emptyForm = {
  fullName: "",
  nationalId: "",
  email: "",
  phone: "",
  shareQuantity: "",
  pricePerShare: "",
  paymentInfo: "",
  submittedVia: "Online",
};

export default function SharePurchase() {
  const { user } = useAuth();
  const [transactions, setTransactions] = useState<ShareTransaction[]>([]);
  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function loadTransactions() {
    const { data } = await api.get<ShareTransaction[]>("/share-transactions");
    setTransactions(data);
  }

  useEffect(() => {
    loadTransactions();
  }, []);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccess(null);
    setLoading(true);
    try {
      await api.post("/share-transactions", {
        shareholder: {
          fullName: form.fullName,
          nationalId: form.nationalId || undefined,
          email: form.email || undefined,
          phone: form.phone || undefined,
        },
        shareQuantity: Number(form.shareQuantity),
        pricePerShare: Number(form.pricePerShare),
        paymentInfo: form.paymentInfo,
        submittedVia: form.submittedVia,
      });
      setForm(emptyForm);
      setSuccess("Purchase request recorded. Proceed to KYC/payment verification below.");
      await loadTransactions();
    } catch (err) {
      setError(apiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }

  async function verify(id: number, approve: boolean) {
    setError(null);
    try {
      await api.post(`/share-transactions/${id}/verify`, { approve });
      await loadTransactions();
    } catch (err) {
      setError(apiErrorMessage(err));
    }
  }

  const canAct = user?.role === "EQUITY_OFFICER" || user?.role === "SYSTEM_ADMIN";

  return (
    <div>
      <h1 className="page-title">1. Share Purchase</h1>
      <p className="page-subtitle">
        Submit a shareholder's purchase request (paper or online) and verify KYC, payment, and documents.
      </p>

      {error && <div className="error-banner">{error}</div>}
      {success && <div className="success-banner">{success}</div>}

      {canAct && (
        <div className="card">
          <h2>Submit Share Purchase Request</h2>
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div>
                <label>Shareholder Full Name</label>
                <input required value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} />
              </div>
              <div>
                <label>National ID</label>
                <input value={form.nationalId} onChange={(e) => setForm({ ...form, nationalId: e.target.value })} />
              </div>
              <div>
                <label>Email</label>
                <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
              </div>
              <div>
                <label>Phone</label>
                <input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
              </div>
              <div>
                <label>Share Quantity</label>
                <input
                  required
                  type="number"
                  min={1}
                  value={form.shareQuantity}
                  onChange={(e) => setForm({ ...form, shareQuantity: e.target.value })}
                />
              </div>
              <div>
                <label>Price Per Share</label>
                <input
                  required
                  type="number"
                  min={0}
                  step="0.01"
                  value={form.pricePerShare}
                  onChange={(e) => setForm({ ...form, pricePerShare: e.target.value })}
                />
              </div>
              <div>
                <label>Payment Info</label>
                <input
                  required
                  placeholder="e.g. Bank transfer ref #..."
                  value={form.paymentInfo}
                  onChange={(e) => setForm({ ...form, paymentInfo: e.target.value })}
                />
              </div>
              <div>
                <label>Submitted Via</label>
                <select value={form.submittedVia} onChange={(e) => setForm({ ...form, submittedVia: e.target.value })}>
                  <option value="Online">Online</option>
                  <option value="Paper">Paper</option>
                </select>
              </div>
            </div>
            <div className="form-actions">
              <button type="submit" disabled={loading}>
                {loading ? "Submitting..." : "Submit Purchase Request"}
              </button>
            </div>
          </form>
        </div>
      )}

      <div className="card">
        <h2>Purchase Requests</h2>
        {transactions.length === 0 ? (
          <div className="empty-state">No purchase requests yet.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Transaction No.</th>
                <th>Shareholder</th>
                <th>Shares</th>
                <th>Payment</th>
                <th>Via</th>
                <th>Status</th>
                <th>Verified By</th>
                {canAct && <th>Actions</th>}
              </tr>
            </thead>
            <tbody>
              {transactions.map((tx) => (
                <tr key={tx.id}>
                  <td>{tx.transactionNo}</td>
                  <td>{tx.shareholder.fullName}</td>
                  <td>{tx.shareQuantity.toLocaleString()}</td>
                  <td>{tx.paymentInfo}</td>
                  <td>{tx.submittedVia}</td>
                  <td>
                    <StatusBadge status={tx.status} />
                  </td>
                  <td>{tx.verifiedBy?.name ?? "—"}</td>
                  {canAct && (
                    <td>
                      {tx.status === "PENDING_VERIFICATION" && (
                        <>
                          <button className="ghost" onClick={() => verify(tx.id, true)}>
                            Verify
                          </button>
                          <button className="ghost" onClick={() => verify(tx.id, false)}>
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
