import { Router } from "express";
import { z } from "zod";
import { prisma } from "../prisma";
import { requireAuth, requireRole } from "../middleware/auth";
import { generateTransactionNo } from "../utils/idGenerators";
import { logActivity } from "../services/activityLog.service";

export const sharePurchaseRouter = Router();
sharePurchaseRouter.use(requireAuth);

const submitSchema = z.object({
  shareholder: z.object({
    id: z.number().optional(),
    fullName: z.string().min(1),
    nationalId: z.string().optional(),
    email: z.string().email().optional().or(z.literal("")),
    phone: z.string().optional(),
    address: z.string().optional(),
  }),
  shareQuantity: z.number().int().positive(),
  pricePerShare: z.number().positive(),
  paymentInfo: z.string().min(1),
  submittedVia: z.enum(["Paper", "Online"]),
});

// Stage 1: Equity Officer records a shareholder's purchase request (paper/online).
sharePurchaseRouter.post("/", requireRole("EQUITY_OFFICER", "SYSTEM_ADMIN"), async (req, res) => {
  const parsed = submitSchema.safeParse(req.body);
  if (!parsed.success) {
    return res.status(400).json({ error: parsed.error.flatten() });
  }
  const { shareholder, shareQuantity, pricePerShare, paymentInfo, submittedVia } = parsed.data;

  const shareholderRecord = shareholder.id
    ? await prisma.shareholder.update({ where: { id: shareholder.id }, data: shareholder })
    : await prisma.shareholder.create({ data: shareholder });

  const transaction = await prisma.shareTransaction.create({
    data: {
      transactionNo: generateTransactionNo(),
      shareholderId: shareholderRecord.id,
      shareQuantity,
      pricePerShare,
      paymentInfo,
      submittedVia,
    },
    include: { shareholder: true },
  });

  await logActivity(req.user!.userId, "SUBMIT_PURCHASE_REQUEST", "ShareTransaction", transaction.id);
  res.status(201).json(transaction);
});

sharePurchaseRouter.get("/", async (req, res) => {
  const transactions = await prisma.shareTransaction.findMany({
    include: { shareholder: true, verifiedBy: { select: { id: true, name: true } } },
    orderBy: { createdAt: "desc" },
  });
  res.json(transactions);
});

sharePurchaseRouter.get("/:id", async (req, res) => {
  const transaction = await prisma.shareTransaction.findUnique({
    where: { id: Number(req.params.id) },
    include: { shareholder: true, verifiedBy: { select: { id: true, name: true } } },
  });
  if (!transaction) return res.status(404).json({ error: "Transaction not found" });
  res.json(transaction);
});

// Equity Officer verifies KYC/payment/documents for the purchase request.
sharePurchaseRouter.post(
  "/:id/verify",
  requireRole("EQUITY_OFFICER", "SYSTEM_ADMIN"),
  async (req, res) => {
    const id = Number(req.params.id);
    const approve = req.body.approve !== false;

    const existing = await prisma.shareTransaction.findUnique({ where: { id } });
    if (!existing) return res.status(404).json({ error: "Transaction not found" });
    if (existing.status !== "PENDING_VERIFICATION") {
      return res.status(409).json({ error: "Transaction already processed" });
    }

    const transaction = await prisma.shareTransaction.update({
      where: { id },
      data: {
        status: approve ? "VERIFIED" : "REJECTED",
        verifiedById: req.user!.userId,
        verifiedAt: new Date(),
      },
      include: { shareholder: true },
    });

    await logActivity(
      req.user!.userId,
      approve ? "VERIFY_PURCHASE_REQUEST" : "REJECT_PURCHASE_REQUEST",
      "ShareTransaction",
      id
    );
    res.json(transaction);
  }
);
