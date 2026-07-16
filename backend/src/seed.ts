import bcrypt from "bcryptjs";
import { prisma } from "./prisma";

async function upsertUser(name: string, email: string, password: string, role: "EQUITY_OFFICER" | "APPROVER" | "SYSTEM_ADMIN" | "INQUIRY_USER") {
  const hash = await bcrypt.hash(password, 10);
  return prisma.user.upsert({
    where: { email },
    update: {},
    create: { name, email, password: hash, role },
  });
}

async function main() {
  await upsertUser("Aye Aye (Equity Officer)", "equity.officer@cbbank.test", "password123", "EQUITY_OFFICER");
  await upsertUser("Ko Ko (Approver)", "approver@cbbank.test", "password123", "APPROVER");
  await upsertUser("Admin", "admin@cbbank.test", "password123", "SYSTEM_ADMIN");
  await upsertUser("Su Su (Inquiry User)", "inquiry@cbbank.test", "password123", "INQUIRY_USER");

  console.log("Seed complete. Demo accounts (password: password123):");
  console.log(" - equity.officer@cbbank.test (EQUITY_OFFICER)");
  console.log(" - approver@cbbank.test (APPROVER)");
  console.log(" - admin@cbbank.test (SYSTEM_ADMIN)");
  console.log(" - inquiry@cbbank.test (INQUIRY_USER)");
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
