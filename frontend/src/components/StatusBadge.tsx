const COLOR_MAP: Record<string, string> = {
  PENDING_VERIFICATION: "amber",
  PENDING_APPROVAL: "amber",
  REQUESTED: "amber",
  VERIFIED: "blue",
  APPROVED: "blue",
  ISSUED: "green",
  PRINTED: "green",
  REJECTED: "red",
  CANCELLED: "red",
  REPLACED: "gray",
  REISSUED: "gray",
};

export default function StatusBadge({ status }: { status: string }) {
  const color = COLOR_MAP[status] ?? "gray";
  return <span className={`badge ${color}`}>{status.replace(/_/g, " ")}</span>;
}
