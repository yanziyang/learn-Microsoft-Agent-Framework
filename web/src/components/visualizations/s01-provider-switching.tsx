"use client";

import { motion } from "framer-motion";
import { useSteppedVisualization } from "@/hooks/useSteppedVisualization";
import { StepControls } from "@/components/visualizations/shared/step-controls";
import { useSvgPalette } from "@/hooks/useDarkMode";

// -- Provider definitions --

interface Provider {
  id: string;
  name: string;
  endpoint: string;
  color: string;
  activeColor: string;
  darkColor: string;
  darkActiveColor: string;
}

const PROVIDERS: Provider[] = [
  {
    id: "openai",
    name: "OpenAI",
    endpoint: "api.openai.com/v1",
    color: "border-emerald-300 bg-emerald-50",
    activeColor: "border-emerald-500 bg-emerald-100 ring-2 ring-emerald-400",
    darkColor: "dark:border-zinc-700 dark:bg-zinc-800/50",
    darkActiveColor: "dark:border-emerald-500 dark:bg-emerald-950/40 dark:ring-emerald-500",
  },
  {
    id: "anthropic",
    name: "Anthropic",
    endpoint: "api.anthropic.com/v1",
    color: "border-violet-300 bg-violet-50",
    activeColor: "border-violet-500 bg-violet-100 ring-2 ring-violet-400",
    darkColor: "dark:border-zinc-700 dark:bg-zinc-800/50",
    darkActiveColor: "dark:border-violet-500 dark:bg-violet-950/40 dark:ring-violet-500",
  },
  {
    id: "ollama",
    name: "Ollama (Local)",
    endpoint: "localhost:11434/v1",
    color: "border-amber-300 bg-amber-50",
    activeColor: "border-amber-500 bg-amber-100 ring-2 ring-amber-400",
    darkColor: "dark:border-zinc-700 dark:bg-zinc-800/50",
    darkActiveColor: "dark:border-amber-500 dark:bg-amber-950/40 dark:ring-amber-500",
  },
  {
    id: "azure",
    name: "Azure OpenAI",
    endpoint: "your-resource.openai.azure.com",
    color: "border-sky-300 bg-sky-50",
    activeColor: "border-sky-500 bg-sky-100 ring-2 ring-sky-400",
    darkColor: "dark:border-zinc-700 dark:bg-zinc-800/50",
    darkActiveColor: "dark:border-sky-500 dark:bg-sky-950/40 dark:ring-sky-500",
  },
];

// Steps: which provider is active (-1 = none, 4 = all)
const ACTIVE_PROVIDER_PER_STEP: number[] = [-1, -1, 0, 1, 2, 3, 4];

const CONFIG_PER_STEP: (string | null)[] = [
  null,
  null,
  "# OpenAI (default)\nbaseUrl=https://api.openai.com/v1\nmodelId=gpt-4o-mini",
  "# Anthropic\nbaseUrl=https://api.anthropic.com/v1\nmodelId=claude-sonnet-4-20250514",
  "# Ollama (Local)\nbaseUrl=http://localhost:11434/v1\nmodelId=llama3",
  "# Azure OpenAI\nbaseUrl=https://your-resource.openai.azure.com\nmodelId=gpt-4o-mini",
  "# No code change — just config\nbaseUrl=...  modelId=...  apiKey=...",
];

const STEP_INFO = [
  { title: "The Interface", desc: "IChatClient defines the contract. Your app code never knows which provider is behind it." },
  { title: "Your App Code", desc: "Application code calls IChatClient.GetResponseAsync() — provider-agnostic, always identical." },
  { title: "→ OpenAI", desc: "new ChatClient(...).AsIChatClient() wraps the OpenAI SDK behind the same interface." },
  { title: "→ Anthropic", desc: "AnthropicClient(...).Messages.AsIChatClient() — same interface, different backend. Zero code change." },
  { title: "→ Ollama (Local)", desc: "OllamaApiClient(...).AsIChatClient() — runs against a local model, no API key needed." },
  { title: "→ Azure OpenAI", desc: "AzureOpenAIClient(...).GetChatClient(model).AsIChatClient() — enterprise endpoint." },
  { title: "One Interface, Any Backend", desc: "Switching providers = editing appsettings.json. No recompile, no code change, no risk." },
];

// SVG layout constants
const SVG_WIDTH = 640;
const SVG_HEIGHT = 360;

const APP_X = SVG_WIDTH / 2;
const APP_Y = 40;
const APP_W = 200;
const APP_H = 48;

const INTERFACE_X = SVG_WIDTH / 2;
const INTERFACE_Y = 150;
const INTERFACE_W = 190;
const INTERFACE_H = 48;

const PROVIDER_Y = 310;
const PROVIDER_W = 120;
const PROVIDER_H = 56;
const PROVIDER_GAP = 20;

function getProviderX(index: number): number {
  const totalWidth = PROVIDERS.length * PROVIDER_W + (PROVIDERS.length - 1) * PROVIDER_GAP;
  const startX = (SVG_WIDTH - totalWidth) / 2;
  return startX + index * (PROVIDER_W + PROVIDER_GAP) + PROVIDER_W / 2;
}

const ACTIVE_COLORS = ["#10b981", "#8b5cf6", "#f59e0b", "#0ea5e9"];
const ACTIVE_BORDER_COLORS = ["#059669", "#7c3aed", "#d97706", "#0284c7"];
const GLOW_FILTERS = [
  "url(#glow-emerald)",
  "url(#glow-violet)",
  "url(#glow-amber)",
  "url(#glow-sky)",
];

export default function ProviderSwitching({ title }: { title?: string }) {
  const {
    currentStep,
    totalSteps,
    next,
    prev,
    reset,
    isPlaying,
    toggleAutoPlay,
  } = useSteppedVisualization({ totalSteps: 7, autoPlayInterval: 2500 });

  const palette = useSvgPalette();
  const activeProviderIdx = ACTIVE_PROVIDER_PER_STEP[currentStep];
  const config = CONFIG_PER_STEP[currentStep];
  const stepInfo = STEP_INFO[currentStep];
  const isAllActive = activeProviderIdx === 4;
  const isAppPhase = currentStep <= 1;
  const isInterfacePhase = currentStep >= 0 && currentStep <= 1;

  return (
    <section className="min-h-[500px] space-y-4">
      <h2 className="text-xl font-semibold text-zinc-900 dark:text-zinc-100">
        {title || "IChatClient Provider Switching"}
      </h2>

      <div className="rounded-lg border border-zinc-200 bg-white p-4 dark:border-zinc-700 dark:bg-zinc-900">
        {/* Config snippet */}
        <div className="mb-4 flex min-h-[60px] items-start gap-2">
          <span className="shrink-0 pt-0.5 text-xs font-medium text-zinc-500 dark:text-zinc-400">
            appsettings.json:
          </span>
          <motion.pre
            key={config ?? "empty"}
            initial={{ opacity: 0, y: -6 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.3 }}
            className="flex-1 overflow-x-auto rounded-md bg-zinc-100 px-3 py-2 font-mono text-[11px] leading-relaxed text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300"
          >
            {config ?? "waiting..."}
          </motion.pre>
        </div>

        {/* SVG diagram */}
        <svg
          viewBox={`0 0 ${SVG_WIDTH} ${SVG_HEIGHT}`}
          className="w-full rounded-md border border-zinc-100 bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-950"
          style={{ minHeight: 260 }}
        >
          <defs>
            <filter id="glow-blue">
              <feDropShadow dx="0" dy="0" stdDeviation="4" floodColor="#3b82f6" floodOpacity="0.7" />
            </filter>
            <filter id="glow-emerald">
              <feDropShadow dx="0" dy="0" stdDeviation="4" floodColor="#10b981" floodOpacity="0.7" />
            </filter>
            <filter id="glow-violet">
              <feDropShadow dx="0" dy="0" stdDeviation="4" floodColor="#8b5cf6" floodOpacity="0.7" />
            </filter>
            <filter id="glow-amber">
              <feDropShadow dx="0" dy="0" stdDeviation="4" floodColor="#f59e0b" floodOpacity="0.7" />
            </filter>
            <filter id="glow-sky">
              <feDropShadow dx="0" dy="0" stdDeviation="4" floodColor="#0ea5e9" floodOpacity="0.7" />
            </filter>
            <marker
              id="provider-arrow"
              markerWidth="8"
              markerHeight="6"
              refX="8"
              refY="3"
              orient="auto"
            >
              <polygon points="0 0, 8 3, 0 6" fill={palette.arrowFill} />
            </marker>
            <marker
              id="provider-arrow-active"
              markerWidth="8"
              markerHeight="6"
              refX="8"
              refY="3"
              orient="auto"
            >
              <polygon points="0 0, 8 3, 0 6" fill={palette.activeEdgeStroke} />
            </marker>
          </defs>

          {/* App code box */}
          <motion.rect
            x={APP_X - APP_W / 2}
            y={APP_Y - APP_H / 2}
            width={APP_W}
            height={APP_H}
            rx={10}
            strokeWidth={2}
            animate={{
              fill: isAppPhase ? palette.activeNodeFill : palette.nodeFill,
              stroke: isAppPhase ? palette.activeNodeStroke : palette.nodeStroke,
            }}
            filter={isAppPhase ? "url(#glow-blue)" : "none"}
            transition={{ duration: 0.4 }}
          />
          <motion.text
            x={APP_X}
            y={APP_Y - 4}
            textAnchor="middle"
            dominantBaseline="middle"
            fontSize={11}
            fontWeight={700}
            fontFamily="monospace"
            animate={{ fill: isAppPhase ? palette.activeNodeText : palette.nodeText }}
            transition={{ duration: 0.4 }}
          >
            {"Your App"}
          </motion.text>
          <motion.text
            x={APP_X}
            y={APP_Y + 12}
            textAnchor="middle"
            dominantBaseline="middle"
            fontSize={9}
            fontFamily="monospace"
            animate={{ fill: isAppPhase ? "rgba(255,255,255,0.75)" : palette.labelFill }}
            transition={{ duration: 0.4 }}
          >
            IChatClient.GetResponseAsync(...)
          </motion.text>

          {/* Line: App → IChatClient */}
          <motion.line
            x1={APP_X}
            y1={APP_Y + APP_H / 2}
            x2={INTERFACE_X}
            y2={INTERFACE_Y - INTERFACE_H / 2}
            strokeWidth={isAppPhase ? 2.5 : 1.5}
            markerEnd="url(#provider-arrow)"
            animate={{
              stroke: isAppPhase ? palette.activeEdgeStroke : palette.edgeStroke,
              strokeWidth: isAppPhase ? 2.5 : 1.5,
            }}
            transition={{ duration: 0.4 }}
          />

          {/* IChatClient interface box */}
          <motion.rect
            x={INTERFACE_X - INTERFACE_W / 2}
            y={INTERFACE_Y - INTERFACE_H / 2}
            width={INTERFACE_W}
            height={INTERFACE_H}
            rx={10}
            strokeWidth={2}
            strokeDasharray={isInterfacePhase && currentStep >= 1 ? "6 3" : "none"}
            animate={{
              fill: isInterfacePhase ? palette.activeNodeFill : palette.nodeFill,
              stroke: isInterfacePhase ? palette.activeNodeStroke : palette.nodeStroke,
            }}
            filter={isInterfacePhase && currentStep >= 1 ? "url(#glow-blue)" : "none"}
            transition={{ duration: 0.4 }}
          />
          <motion.text
            x={INTERFACE_X}
            y={INTERFACE_Y - 4}
            textAnchor="middle"
            dominantBaseline="middle"
            fontSize={13}
            fontWeight={700}
            fontFamily="monospace"
            animate={{
              fill: isInterfacePhase ? palette.activeNodeText : palette.nodeText,
            }}
            transition={{ duration: 0.4 }}
          >
            {"IChatClient"}
          </motion.text>
          <motion.text
            x={INTERFACE_X}
            y={INTERFACE_Y + 12}
            textAnchor="middle"
            dominantBaseline="middle"
            fontSize={8}
            fontFamily="sans-serif"
            animate={{
              fill: isInterfacePhase ? "rgba(255,255,255,0.7)" : palette.labelFill,
            }}
            transition={{ duration: 0.4 }}
          >
            {"(interface contract)"}
          </motion.text>

          {/* Lines from IChatClient to each provider */}
          {PROVIDERS.map((provider, i) => {
            const providerX = getProviderX(i);
            const isActive = isAllActive || i === activeProviderIdx;
            const lineColor = isActive ? palette.activeEdgeStroke : palette.edgeStroke;

            return (
              <motion.line
                key={`line-${provider.id}`}
                x1={INTERFACE_X}
                y1={INTERFACE_Y + INTERFACE_H / 2}
                x2={providerX}
                y2={PROVIDER_Y - PROVIDER_H / 2}
                strokeWidth={isActive ? 2.5 : 1.5}
                markerEnd={isActive ? "url(#provider-arrow-active)" : "url(#provider-arrow)"}
                animate={{ stroke: lineColor, strokeWidth: isActive ? 2.5 : 1.5 }}
                transition={{ duration: 0.4 }}
              />
            );
          })}

          {/* Provider cards */}
          {PROVIDERS.map((provider, i) => {
            const providerX = getProviderX(i);
            const isActive = isAllActive || i === activeProviderIdx;

            return (
              <g key={provider.id}>
                <motion.rect
                  x={providerX - PROVIDER_W / 2}
                  y={PROVIDER_Y - PROVIDER_H / 2}
                  width={PROVIDER_W}
                  height={PROVIDER_H}
                  rx={8}
                  strokeWidth={2}
                  animate={{
                    fill: isActive ? ACTIVE_COLORS[i] : palette.nodeFill,
                    stroke: isActive ? ACTIVE_BORDER_COLORS[i] : palette.nodeStroke,
                  }}
                  filter={isActive ? GLOW_FILTERS[i] : "none"}
                  transition={{ duration: 0.4 }}
                />
                <motion.text
                  x={providerX}
                  y={PROVIDER_Y - 8}
                  textAnchor="middle"
                  dominantBaseline="middle"
                  fontSize={11}
                  fontWeight={700}
                  fontFamily="monospace"
                  animate={{ fill: isActive ? "#ffffff" : palette.nodeText }}
                  transition={{ duration: 0.4 }}
                >
                  {provider.name}
                </motion.text>
                <motion.text
                  x={providerX}
                  y={PROVIDER_Y + 10}
                  textAnchor="middle"
                  dominantBaseline="middle"
                  fontSize={7.5}
                  fontFamily="sans-serif"
                  animate={{ fill: isActive ? "rgba(255,255,255,0.8)" : palette.labelFill }}
                  transition={{ duration: 0.4 }}
                >
                  {provider.endpoint}
                </motion.text>
              </g>
            );
          })}

          {/* ".AsIChatClient()" annotation on provider steps */}
          {currentStep >= 2 && currentStep <= 5 && (
            <motion.g
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              transition={{ duration: 0.4 }}
            >
              <rect
                x={getProviderX(activeProviderIdx) - 52}
                y={PROVIDER_Y - PROVIDER_H / 2 - 26}
                width={104}
                height={20}
                rx={4}
                fill={ACTIVE_COLORS[activeProviderIdx]}
                opacity={0.9}
              />
              <text
                x={getProviderX(activeProviderIdx)}
                y={PROVIDER_Y - PROVIDER_H / 2 - 14}
                textAnchor="middle"
                dominantBaseline="middle"
                fontSize={8}
                fontWeight={600}
                fontFamily="monospace"
                fill="#ffffff"
              >
                .AsIChatClient()
              </text>
            </motion.g>
          )}
        </svg>

        {/* Code hint for the final "all active" step */}
        {isAllActive && (
          <div className="mt-3 rounded-md bg-zinc-100 px-3 py-2 dark:bg-zinc-800">
            <code className="block font-mono text-[11px] leading-relaxed text-zinc-600 dark:text-zinc-300">
              <span className="text-blue-600 dark:text-blue-400">IChatClient</span>
              {" chat = "}
              <span className="text-emerald-600 dark:text-emerald-400">provider</span>
              {".AsIChatClient();  "}
              <span className="text-zinc-400">{"// same code, any backend"}</span>
            </code>
          </div>
        )}
      </div>

      <StepControls
        currentStep={currentStep}
        totalSteps={totalSteps}
        onPrev={prev}
        onNext={next}
        onReset={reset}
        isPlaying={isPlaying}
        onToggleAutoPlay={toggleAutoPlay}
        stepTitle={stepInfo.title}
        stepDescription={stepInfo.desc}
      />
    </section>
  );
}
