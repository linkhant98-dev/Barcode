import { useEffect, useState } from "react";
import { api, apiErrorMessage } from "../api/client";
import { Certificate } from "../types";
import StatusBadge from "../components/StatusBadge";
import { useAuth } from "../context/AuthContext";

export default function CertificateManagement() {
  const { user } = useAuth();
  const [query, setQuery] = useState("");
  const [certificates, setCertificates] = useState<Certificate[]>([]);
  const [selected, setSelected] = useState<Certificate | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [newQty, setNewQty] = useState("");
  const [reasonNote, setReasonNote] = useState("");

  async function load(q = query) {
    const { data } = await api.get<Certificate[]>("/certificates", { params: { q } });
    setCertificates(data);
  }

  useEffect(() => {
    load("");
  }, []);

  async function openDetail(id: number) {
    setError(null);
    const { data } = await api.get<Certificate>(`/certificates/${id}`);
    setSelected(data);
    setNewQty(String(data.shareQuantity));
    setReasonNote("");
  }

  async function refreshSelected() {
    if (selected) await openDetail(selected.id);
    await load();
  }

  async function handleUpdate() {
    if (!selected) return;
    setError(null);
    try {
      await api.patch(`/certificates/${selected.id}`, {
        shareQuantity: Number(newQty),
        notes: reasonNote || undefined,
      });
      await refreshSelected();
    } catch (err) {
      setError(apiErrorMessage(err));
    }
  }

  async function handleAction(action: "cancel" | "replace" | "reissue") {
    if (!selected) return;
    setError(null);
    try {
      await api.post(`/certificates/${selected.id}/${action}`, { reason: reasonNote || undefined });
      await refreshSelected();
    } catch (err) {
      setError(apiErrorMessage(err));
    }
  }

  const canManage = user?.role === "EQUITY_OFFICER" || user?.role === "SYSTEM_ADMIN";

  return (
    <div>
      <h1 className="page-title">3. Certificate Management</h1>
      <p className="page-subtitle">Search and view shareholder &amp; certificate information; update, cancel, replace, or reissue as required.</p>

      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <div className="toolbar">
          <input
            placeholder="Search by certificate no. or shareholder name..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && load()}
            style={{ maxWidth: 360 }}
          />
          <button onClick={() => load()}>Search</button>
        </div>

        {certificates.length === 0 ? (
          <div className="empty-state">No certificates found.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Certificate No.</th>
                <th>Shareholder</th>
                <th>Shares</th>
                <th>Issue Date</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {certificates.map((c) => (
                <tr key={c.id}>
                  <td>{c.certificateNo}</td>
                  <td>{c.shareholder.fullName}</td>
                  <td>{c.shareQuantity.toLocaleString()}</td>
                  <td>{new Date(c.issueDate).toLocaleDateString()}</td>
                  <td>
                    <StatusBadge status={c.status} />
                  </td>
                  <td>
                    <button className="ghost" onClick={() => openDetail(c.id)}>
                      View / Manage
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {selected && (
        <div className="modal-backdrop" onClick={() => setSelected(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>{selected.certificateNo}</h2>
            <p style={{ color: "#6b7385", marginTop: -8 }}>
              {selected.shareholder.fullName} · <StatusBadge status={selected.status} />
            </p>

            {canManage && selected.status === "ISSUED" && (
              <>
                <label>Share Quantity</label>
                <input type="number" min={1} value={newQty} onChange={(e) => setNewQty(e.target.value)} />
                <label>Note / Reason (applies to update, cancel, replace, reissue)</label>
                <textarea rows={2} value={reasonNote} onChange={(e) => setReasonNote(e.target.value)} />
                <div className="form-actions" style={{ flexWrap: "wrap" }}>
                  <button onClick={handleUpdate}>Save Update</button>
                  <button className="secondary" onClick={() => handleAction("replace")}>
                    Replace
                  </button>
                  <button className="secondary" onClick={() => handleAction("reissue")}>
                    Reissue
                  </button>
                  <button className="danger" onClick={() => handleAction("cancel")}>
                    Cancel Certificate
                  </button>
                </div>
              </>
            )}

            <h3 style={{ marginBottom: 6, fontSize: 13.5 }}>Status &amp; History</h3>
            <table>
              <thead>
                <tr>
                  <th>Action</th>
                  <th>Notes</th>
                  <th>By</th>
                  <th>When</th>
                </tr>
              </thead>
              <tbody>
                {(selected.history ?? []).map((h) => (
                  <tr key={h.id}>
                    <td>{h.action}</td>
                    <td>{h.notes ?? "—"}</td>
                    <td>{h.performedBy.name}</td>
                    <td>{new Date(h.createdAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div className="form-actions">
              <button className="secondary" onClick={() => setSelected(null)}>
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
