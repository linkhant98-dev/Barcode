import { Router } from "express";
import { z } from "zod";
import { prisma } from "../prisma";
import { requireAuth, requireRole } from "../middleware/auth";
import { generateCertificateNo } from "../utils/idGenerators";
import { logActivity } from "../services/activityLog.service";

export const certificateRouter = Router();
certificateRouter.use(requireAuth);

const certificateInclude = {
  shareholder: true,
  history: { orderBy: { createdAt: "desc" as const }, include: { performedBy: { select: { id: true, name: true } } } },
  replacesCertificate: true,
  replacedByCertificate: true,
};

// Search / view shareholder & certificate information.
certificateRouter.get("/", async (req, res) => {
  const q = (req.query.q as string) || "";
  const certificates = await prisma.certificate.findMany({
    where: q
      ? {
          OR: [
            { certificateNo: { contains: q } },
            { shareholder: { fullName: { contains: q } } },
          ],
        }
      : undefined,
    include: certificateInclude,
    orderBy: { issueDate: "desc" },
  });
  res.json(certificates);
});

certificateRouter.get("/:id", async (req, res) => {
  const certificate = await prisma.certificate.findUnique({
    where: { id: Number(req.params.id) },
    include: certificateInclude,
  });
  if (!certificate) return res.status(404).json({ error: "Certificate not found" });
  res.json(certificate);
});

const updateSchema = z.object({
  shareQuantity: z.number().int().positive().optional(),
  notes: z.string().optional(),
});

// Update certificate data (if required).
certificateRouter.patch("/:id", requireRole("EQUITY_OFFICER", "SYSTEM_ADMIN"), async (req, res) => {
  const id = Number(req.params.id);
  const parsed = updateSchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json({ error: parsed.error.flatten() });

  const existing = await prisma.certificate.findUnique({ where: { id } });
  if (!existing) return res.status(404).json({ error: "Certificate not found" });
  if (existing.status !== "ISSUED") {
    return res.status(409).json({ error: "Only issued certificates can be updated" });
  }

  const certificate = await prisma.$transaction(async (tx) => {
    const updated = await tx.certificate.update({
      where: { id },
      data: { shareQuantity: parsed.data.shareQuantity ?? existing.shareQuantity },
    });
    await tx.certificateHistory.create({
      data: {
        certificateId: id,
        action: "UPDATED",
        notes: parsed.data.notes ?? "Certificate data updated",
        performedById: req.user!.userId,
      },
    });
    return updated;
  });

  await logActivity(req.user!.userId, "UPDATE_CERTIFICATE", "Certificate", id);
  res.json(certificate);
});

// Cancel a certificate.
certificateRouter.post("/:id/cancel", requireRole("EQUITY_OFFICER", "SYSTEM_ADMIN"), async (req, res) => {
  const id = Number(req.params.id);
  const existing = await prisma.certificate.findUnique({ where: { id } });
  if (!existing) return res.status(404).json({ error: "Certificate not found" });
  if (existing.status !== "ISSUED") {
    return res.status(409).json({ error: "Only issued certificates can be cancelled" });
  }

  const certificate = await prisma.$transaction(async (tx) => {
    const updated = await tx.certificate.update({ where: { id }, data: { status: "CANCELLED" } });
    await tx.certificateHistory.create({
      data: {
        certificateId: id,
        action: "CANCELLED",
        notes: req.body.reason ?? "Certificate cancelled",
        performedById: req.user!.userId,
      },
    });
    return updated;
  });

  await logActivity(req.user!.userId, "CANCEL_CERTIFICATE", "Certificate", id);
  res.json(certificate);
});

type ReissueOutcome =
  | { error: string; status: number }
  | { data: { oldCert: unknown; newCert: unknown } };

// Shared helper: replace/reissue both retire the old certificate and mint a linked new one.
async function reissueCertificate(
  certificateId: number,
  performedById: number,
  action: "REPLACED" | "REISSUED",
  reason?: string
): Promise<ReissueOutcome> {
  const existing = await prisma.certificate.findUnique({ where: { id: certificateId } });
  if (!existing) return { error: "Certificate not found", status: 404 };
  if (existing.status !== "ISSUED") {
    return { error: `Only issued certificates can be ${action.toLowerCase()}`, status: 409 };
  }

  const newCertificateNo = generateCertificateNo();
  const result = await prisma.$transaction(async (tx) => {
    const oldCert = await tx.certificate.update({ where: { id: certificateId }, data: { status: action } });

    const newCert = await tx.certificate.create({
      data: {
        certificateNo: newCertificateNo,
        barcodeValue: newCertificateNo,
        certificateRequestId: existing.certificateRequestId,
        shareholderId: existing.shareholderId,
        shareQuantity: existing.shareQuantity,
        replacesCertificateId: existing.id,
      },
    });

    await tx.certificateHistory.create({
      data: {
        certificateId: existing.id,
        action,
        notes: reason ?? `${action === "REPLACED" ? "Replaced" : "Reissued"} by certificate ${newCertificateNo}`,
        performedById,
      },
    });
    await tx.certificateHistory.create({
      data: {
        certificateId: newCert.id,
        action: "CREATED",
        notes: `${action === "REPLACED" ? "Replacement" : "Reissue"} for certificate ${existing.certificateNo}`,
        performedById,
      },
    });

    return { oldCert, newCert };
  });

  return { data: result };
}

certificateRouter.post("/:id/replace", requireRole("EQUITY_OFFICER", "SYSTEM_ADMIN"), async (req, res) => {
  const id = Number(req.params.id);
  const outcome = await reissueCertificate(id, req.user!.userId, "REPLACED", req.body.reason);
  if ("error" in outcome) return res.status(outcome.status).json({ error: outcome.error });
  await logActivity(req.user!.userId, "REPLACE_CERTIFICATE", "Certificate", id);
  res.json(outcome.data);
});

certificateRouter.post("/:id/reissue", requireRole("EQUITY_OFFICER", "SYSTEM_ADMIN"), async (req, res) => {
  const id = Number(req.params.id);
  const outcome = await reissueCertificate(id, req.user!.userId, "REISSUED", req.body.reason);
  if ("error" in outcome) return res.status(outcome.status).json({ error: outcome.error });
  await logActivity(req.user!.userId, "REISSUE_CERTIFICATE", "Certificate", id);
  res.json(outcome.data);
});
