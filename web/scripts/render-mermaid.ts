import * as fs from "fs";
import * as path from "path";
import * as os from "os";
import { run } from "@mermaid-js/mermaid-cli";
import { MERMAID_THEME } from "../src/lib/mermaid-theme";

const WEB_DIR = path.resolve(__dirname, "..");
const COURSE_ASSETS_DIR = path.join(WEB_DIR, "public", "course-assets");

const renderedCache = new Map<string, string>();

export async function renderMermaidToSvg(
  mermaidCode: string,
  chapterDirName: string,
  blockIndex: number
): Promise<string> {
  const cacheKey = `${chapterDirName}-${blockIndex}`;
  const cached = renderedCache.get(cacheKey);
  if (cached) return cached;

  const outDir = path.join(COURSE_ASSETS_DIR, chapterDirName);
  fs.mkdirSync(outDir, { recursive: true });

  const outputFilename = `mermaid-${blockIndex}.svg`;
  const outputPath = path.join(outDir, outputFilename);
  const publicPath = `course-assets/${chapterDirName}/${outputFilename}`;

  if (fs.existsSync(outputPath)) {
    renderedCache.set(cacheKey, publicPath);
    return publicPath;
  }

  const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), "mermaid-"));
  try {
    const inputFile = path.join(tempDir, "diagram.mmd");
    fs.writeFileSync(inputFile, mermaidCode, "utf-8");

    await run(inputFile, outputPath as `${string}.svg`, {
      parseMMDOptions: {
        mermaidConfig: JSON.parse(JSON.stringify(MERMAID_THEME)),
      },
    });
  } finally {
    fs.rmSync(tempDir, { recursive: true, force: true });
  }

  renderedCache.set(cacheKey, publicPath);
  return publicPath;
}
