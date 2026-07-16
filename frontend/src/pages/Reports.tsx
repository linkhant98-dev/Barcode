import { useEffect, useState } from "react";
import { api } from "../api/client";
import { ActivityLogEntry, Certificate, PrintJob } from "../types";
import StatusBadge from "../components/StatusBadge";

interface ShareholderReportRow {
  id: number;
  fullName: string;
  nationalId?: string | null;
  email?: string | null;
  phone?: string | null;
  totalIssuedCertificates: number;
  totalShares: number;
}

type ReportKind = "register" | "shareholders" | "print-history" | "activity-log";

const TABS: { key: ReportKind; label: string }[] = [
  { key: "register", label: "Certificate Register" },
  { key: "shareholders", label: "Shareholder List" },
  { key: "print-history", label: "Print History" },
  { key: "activity-log", label: "Activity Log" },
];

export default function Reports() {
  const [tab, setTab] = useState<ReportKind>("register");
  const [certificates, setCertificates] = useState<Certificate[]>([]);
  const [shareholders, setShareholders] = useState<ShareholderReportRow[]>([]);
  const [printJobs, setPrintJobs] = useState<PrintJob[]>([]);
  const [logs, setLogs] = useState<ActivityLogEntry[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    async function load() {
      if (tab === "register") {
        const { data } = await api.get<Certificate[]>("/reports/certificate-register");
        if (!cancelled) setCertificates(data);
      } else if (tab === "shareholders") {
        const { data } = await api.get<ShareholderReportRow[]>("/reports/shareholders");
        if (!cancelled) setShareholders(data);
      } else if (tab === "print-history") {
        const { data } = await api.get<PrintJob[]>("/reports/print-history");
        if (!cancelled) setPrintJobs(data);
      } else {
        const { data } = await api.get<ActivityLogEntry[]>("/reports/activity-log");
        if (!cancelled) setLogs(data);
      }
      if (!cancelled) setLoading(false);
    }
    load();
    return () => {
      cancelled = true;
    };
  }, [tab]);

  return (
    <div>
      <h1 className="page-title">5. Reporting &amp; Inquiry</h1>
      <p className="page-subtitle">Certificate register, shareholder list, print history, and activity/audit log.</p>

      <div className="card">
        <div className="toolbar">
          <div>
            {TABS.map((t) => (
              <button
                key={t.key}
                className={t.key === tab ? "" : "secondary"}
                style={{ marginRight: 8 }}
                onClick={() => setTab(t.key)}
              >
                {t.label}
              </button>
            ))}
          </div>
        </div>

        {loading && <div className="empty-state">Loading...</div>}

        {!loading && tab === "register" && (
          certificates.length === 0 ? (
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
                </tr>
              </thead>
              <tbody>
                {certificates.map((c) => (
                  <tr key={c.id}>
                    <td>{c.certificateNo}</td>
                    <td>{c.shareholder.fullName}</td>
                    <td>{c.shareQuantity.toLocaleString()}</td>
                    <td>{new Date(c.issueDate).toLocaleDateString()}</td>
                    <td><StatusBadge status={c.status} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )
        )}

        {!loading && tab === "shareholders" && (
          shareholders.length === 0 ? (
            <div className="empty-state">No shareholders found.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>National ID</th>
                  <th>Email</th>
                  <th>Phone</th>
                  <th>Issued Certificates</th>
                  <th>Total Shares</th>
                </tr>
              </thead>
              <tbody>
                {shareholders.map((s) => (
                  <tr key={s.id}>
                    <td>{s.fullName}</td>
                    <td>{s.nationalId ?? "—"}</td>
                    <td>{s.email ?? "—"}</td>
                    <td>{s.phone ?? "—"}</td>
                    <td>{s.totalIssuedCertificates}</td>
                    <td>{s.totalShares.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )
        )}

        {!loading && tab === "print-history" && (
          printJobs.length === 0 ? (
            <div className="empty-state">No certificates printed yet.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Certificate No.</th>
                  <th>Shareholder</th>
                  <th>Printed By</th>
                  <th>Printed At</th>
                </tr>
              </thead>
              <tbody>
                {printJobs.map((j) => (
                  <tr key={j.id}>
                    <td>{j.certificate.certificateNo}</td>
                    <td>{j.certificate.shareholder.fullName}</td>
                    <td>{j.printedBy?.name ?? "—"}</td>
                    <td>{j.printedAt ? new Date(j.printedAt).toLocaleString() : "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )
        )}

        {!loading && tab === "activity-log" && (
          logs.length === 0 ? (
            <div className="empty-state">No activity recorded yet.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Action</th>
                  <th>Entity</th>
                  <th>User</th>
                  <th>Role</th>
                  <th>When</th>
                </tr>
              </thead>
              <tbody>
                {logs.map((l) => (
                  <tr key={l.id}>
                    <td>{l.action}</td>
                    <td>{l.entityType} {l.entityId ? `#${l.entityId}` : ""}</td>
                    <td>{l.user?.name ?? "—"}</td>
                    <td>{l.user?.role.replace("_", " ") ?? "—"}</td>
                    <td>{new Date(l.createdAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )
        )}
      </div>
    </div>
  );
}
