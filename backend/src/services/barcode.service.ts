import bwipjs from "bwip-js";

/** Renders a Code128 barcode for the given value as a base64-encoded PNG data URL. */
export async function generateBarcodePng(value: string): Promise<string> {
  const png = await bwipjs.toBuffer({
    bcid: "code128",
    text: value,
    scale: 3,
    height: 12,
    includetext: true,
    textxalign: "center",
  });
  return `data:image/png;base64,${png.toString("base64")}`;
}
