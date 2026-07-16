import { Router } from "express";
import { z } from "zod";
import { prisma } from "../prisma";
import { requireAuth, requireRole } from "../middleware/auth";
import { generateCertificateNo } from "../utils/idGenerators";
import { logActivity } from "../services/activityLog.service";

export const certificateRequestRouter = Router();
certificateRequestRouter.use(requireAuth);

const createSchema = z.object({
  transactionId: z.number().int(),
});

// Stage 2: Equity Officer creates a shareholder certificate request from a verified transaction.
certificateRequestRouter.post("/", requireRole("EQUITY_OFFICER", "SYSTEM_ADMIN"), async (req, res) => {
  const parsed = createSchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json({ error: parsed.error.flatten() });

  const transaction = await prisma.shareTransaction.findUnique({
    where: { id: parsed.data.transactionId },
  });
  if (!transaction) return res.status(404).json({ error: "Transaction not found" });
  if (transaction.status !== "VERIFIED") {
    return res.status(409).json({ error: "Transaction must be verified before requesting a certificate" });
  }

  const request = await prisma.certificateRequest.create({
    data: { transactionId: transaction.id, requestedById: req.user!.userId },
    include: { transaction: { include: { shareholder: true } }, requestedBy: { select: { id: true, name: true } } },
  });

  await logActivity(req.user!.userId, "CREATE_CERTIFICATE_REQUEST", "CertificateRequest", request.id);
  res.status(201).json(request);
});

certificateRequestRouter.get("/", async (req, res) => {
  const requests = await prisma.certificateRequest.findMany({
    include: {
      transaction: { include: { shareholder: true } },
      requestedBy: { select: { id: true, name: true } },
      approvedBy: { select: { id: true, name: true } },
      certificate: true,
    },
    orderBy: { createdAt: "desc" },
  });
  res.json(requests);
});

// Approver (Manager) reviews and approves/rejects the certificate request.
// On approval, the System generates the certificate details and barcode, and issues it.
certificateRequestRouter.post(
  "/:id/decision",
  requireRole("APPROVER", "SYSTEM_ADMIN"),
  async (req, res) => {
    const id = Number(req.params.id);
    const approve = req.body.approve !== false;

    const request = await prisma.certificateRequest.findUnique({
      where: { id },
      include: { transaction: { include: { shareholder: true } } },
    });
    if (!request) return res.status(404).json({ error: "Certificate request not found" });
    if (request.status !== "PENDING_APPROVAL") {
      return res.status(409).json({ error: "Certificate request already decided" });
    }

    if (!approve) {
      const rejected = await prisma.certificateRequest.update({
        where: { id },
        data: {
          status: "REJECTED",
          approvedById: req.user!.userId,
          decidedAt: new Date(),
          rejectionNote: req.body.note ?? null,
        },
      });
      await logActivity(req.user!.userId, "REJECT_CERTIFICATE_REQUEST", "CertificateRequest", id);
      return res.json(rejected);
    }

    const certificateNo = generateCertificateNo();
    const result = await prisma.$transaction(async (tx) => {
      const updatedRequest = await tx.certificateRequest.update({
        where: { id },
        data: { status: "APPROVED", approvedById: req.user!.userId, decidedAt: new Date() },
      });

      const certificate = await tx.certificate.create({
        data: {
          certificateNo,
          barcodeValue: certificateNo,
          certificateRequestId: updatedRequest.id,
          shareholderId: request.transaction.shareholderId,
          shareQuantity: request.transaction.shareQuantity,
        },
        include: { shareholder: true },
      });

      await tx.certificateHistory.create({
        data: {
          certificateId: certificate.id,
          action: "CREATED",
          notes: "Certificate issued from approved certificate request",
          performedById: req.user!.userId,
        },
      });

      return { updatedRequest, certificate };
    });

    await logActivity(req.user!.userId, "APPROVE_CERTIFICATE_REQUEST", "CertificateRequest", id);
    await logActivity(req.user!.userId, "ISSUE_CERTIFICATE", "Certificate", result.certificate.id);

    res.json(result);
  }
);
