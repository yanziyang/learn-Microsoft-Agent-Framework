"use client";

import { motion } from "framer-motion";
import { useSteppedVisualization } from "@/hooks/useSteppedVisualization";
import { StepControls } from "@/components/visualizations/shared/step-controls";
import { useSvgPalette } from "@/hooks/useDarkMode";

// -- Layer definitions --

interface PipelineLayer {
  id: string;
  name: string;
  preAction: string;
  postAction: string;
  color: string;
  activeColor: string;
  glowId: string;
}

const LAYERS: PipelineLayer[] = [
  {
    id: "timing",
    name: "Timing",
    preAction: "sw.Start()",
    postAction: "elapsed = sw.Elapsed",
    color: "border-orange-200 bg-orange-50",
    activeColor: "border-orange-500 bg-orange-100 ring-2 ring-orange-400",
    glowId: "glow-orange",
  },
  {
    id: "logging",
    name: "Logging",
    preAction: "Log(question)",
    postAction: "Log(length)",
    color: "border-sky-200 bg-sky-50",
    activeColor: "border-sky-500 bg-sky-100 ring-2 ring-sky-400",
    glowId: "glow-sky",
  },
  {
    id: "openai",
    name: "OpenAI Client",
    preAction: "→ API call",
    postAction: "← ChatResponse",
    color: "border-violet-200 bg-violet-50",
    activeColor: "border-violet-500 bg-violet-100 ring-2 ring-violet-400",
    glowId: "glow-violet",
  },
];

// Steps: which layer is active, and flow direction
// mode: "struct" = show pipeline, "req-1/2/3" = request down through layers, "res-1/2/3" = response up
const STEP_INFO = [
  { title: "The Onion Layers", desc: "Each Use() wraps the inner client. Outermost sees every call first, innermost reaches the provider." },
  { title: "Request → Timing", desc: "Timing starts a stopwatch before forwarding. It measures total round-trip including all middleware." },
  { title: "Request → Logging", desc: "Logging prints the user's question before forwarding. It only logs — it doesn't modify the request." },
  { title: "Request → OpenAI Client", desc: "The request reaches the core provider. It sends the API call and gets a raw response." },
  { title: "Response ← Logging", desc: "Logging sees the response on the way back and prints its length. Response passes through unchanged." },
  { title: "Response ← Timing", desc: "Timing stops the stopwatch and prints the elapsed time. Total round-trip = all layers combined." },
  { title: "Full Round-Trip", desc: "Request flows outside-in, response flows inside-out. Add or remove layers with one-line pipeline changes." },
];

// SVG layout
const SVG_WIDTH = 640;
const SVG_HEIGHT = 400;
const CENTER_X = SVG_WIDTH / 2;
const LAYER_START_Y = 60;
const LAYER_H = 72;
const LAYER_GAP = 24;
const LAYER_W = 280;
const REQ_X = 80;
const RES_X = SVG_WIDTH - 80;

function getLayerY(index: number): number {
  return LAYER_START_Y + index * (LAYER_H + LAYER_GAP) + LAYER_H / 2;
}

export default function MiddlewarePipeline({ title }: { title?: string }) {
  const {
    currentStep,
    totalSteps,
    next,
    prev,
    reset,
    isPlaying,
    toggleAutoPlay,
  } = useSteppedVisualization({ totalSteps: 7, autoPlayInterval: 2800 });

  const palette = useSvgPalette();
  const stepInfo = STEP_INFO[currentStep];

  // Determine which layer is active and flow state
  const isStruct = currentStep === 0;
  const isReqPhase = currentStep >= 1 && currentStep <= 3;
  const isResPhase = currentStep >= 4 && currentStep <= 5;
  const isAllActive = currentStep === 6;

  // Which layer index is active (0-based), -1 = none
  let activeLayerIdx = -1;
  if (currentStep === 1) activeLayerIdx = 0; // timing
  else if (currentStep === 2) activeLayerIdx = 1; // logging
  else if (currentStep === 3) activeLayerIdx = 2; // openai
  else if (currentStep === 4) activeLayerIdx = 1; // logging (return)
  else if (currentStep === 5) activeLayerIdx = 0; // timing (return)

  const isLayerActive = (idx: number) =>
    isAllActive || idx === activeLayerIdx;

  // Arrow path for request (downward on left side)
  const reqPathDown = (topY: number, botY: number) =>
    `M ${REQ_X} ${topY} L ${REQ_X} ${botY}`;
  // Arrow path for response (upward on right side)
  const resPathUp = (topY: number, botY: number) =>
    `M ${RES_X} ${botY} L ${RES_X} ${topY}`;

  // Highlight request arrows
  const reqArrowActive = (idx: number) => {
    if (isAllActive) return true;
    // Step 1: arrow from top to layer 0
    if (currentStep === 1 && idx === 0) return true;
    // Step 2: arrow from layer 0 to layer 1
    if (currentStep === 2 && idx === 1) return true;
    // Step 3: arrow from layer 1 to layer 2
    if (currentStep === 3 && idx === 2) return true;
    return false;
  };

  // Highlight response arrows
  const resArrowActive = (idx: number) => {
    if (isAllActive) return true;
    // Step 4: arrow from layer 2 to layer 1
    if (currentStep === 4 && idx === 1) return true;
    // Step 5: arrow from layer 1 to layer 0
    if (currentStep === 5 && idx === 0) return true;
    return false;
  };

  const glowFilter = (layer: PipelineLayer) =>
    isAllActive || (activeLayerIdx >= 0 && LAYERS[activeLayerIdx].id === layer.id)
      ? `url(#${layer.glowId})`
      : "none";

  const activeStroke = (idx: number) =>
    reqArrowActive(idx) || resArrowActive(idx)
      ? palette.activeEdgeStroke
      : palette.edgeStroke;

  const arrowColor = (active: boolean) =>
    active ? palette.activeEdgeStroke : palette.edgeStroke;

  return (
    <section className="min-h-[500px] space-y-4">
      <h2 className="text-xl font-semibold text-zinc-900 dark:text-zinc-100">
        {title || "Middleware Pipeline Composition"}
      </h2>

      <div className="rounded-lg border border-zinc-200 bg-white p-4 dark:border-zinc-700 dark:bg-zinc-900">
        {/* Pipeline code */}
        <div className="mb-4">
          <pre className="overflow-x-auto rounded-md bg-zinc-100 px-3 py-2 font-mono text-[11px] leading-relaxed text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
            <span className="text-blue-600 dark:text-blue-400">var</span>
            {" client = innerClient\n    .AsBuilder()\n    .Use("}
            <span className="text-orange-600 dark:text-orange-400">inner {'=> new Timing(inner)'}</span>
            {")\n    .Use("}
            <span className="text-sky-600 dark:text-sky-400">inner {'=> new Logging(inner)'}</span>
            {")\n    .Build();"}
          </pre>
        </div>

        <div className="flex flex-col gap-4 lg:flex-row">
          {/* Left panel: SVG diagram (65%) */}
          <div className="w-full lg:w-[65%]">
            <svg
              viewBox={`0 0 ${SVG_WIDTH} ${SVG_HEIGHT}`}
              className="w-full rounded-md border border-zinc-100 bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-950"
              style={{ minHeight: 300 }}
            >
              <defs>
                <filter id="glow-blue">
                  <feDropShadow dx="0" dy="0" stdDeviation="4" floodColor="#3b82f6" floodOpacity="0.7" />
                </filter>
                <filter id="glow-orange">
                  <feDropShadow dx="0" dy="0" stdDeviation="4" floodColor="#f97316" floodOpacity="0.7" />
                </filter>
                <filter id="glow-sky">
                  <feDropShadow dx="0" dy="0" stdDeviation="4" floodColor="#0ea5e9" floodOpacity="0.7" />
                </filter>
                <filter id="glow-violet">
                  <feDropShadow dx="0" dy="0" stdDeviation="4" floodColor="#8b5cf6" floodOpacity="0.7" />
                </filter>
                <marker
                  id="pipe-arrow-down"
                  markerWidth="8"
                  markerHeight="6"
                  refX="8"
                  refY="3"
                  orient="auto"
                >
                  <polygon points="0 0, 8 3, 0 6" fill={palette.arrowFill} />
                </marker>
                <marker
                  id="pipe-arrow-down-active"
                  markerWidth="8"
                  markerHeight="6"
                  refX="8"
                  refY="3"
                  orient="auto"
                >
                  <polygon points="0 0, 8 3, 0 6" fill={palette.activeEdgeStroke} />
                </marker>
                <marker
                  id="pipe-arrow-up"
                  markerWidth="8"
                  markerHeight="6"
                  refX="0"
                  refY="3"
                  orient="auto"
                >
                  <polygon points="8 0, 0 3, 8 6" fill={palette.arrowFill} />
                </marker>
                <marker
                  id="pipe-arrow-up-active"
                  markerWidth="8"
                  markerHeight="6"
                  refX="0"
                  refY="3"
                  orient="auto"
                >
                  <polygon points="8 0, 0 3, 8 6" fill={palette.activeEdgeStroke} />
                </marker>
              </defs>

              {/* Request label */}
              <text
                x={REQ_X}
                y={22}
                textAnchor="middle"
                fontSize={10}
                fontWeight={600}
                fontFamily="monospace"
                fill={isReqPhase || isAllActive ? palette.activeEdgeStroke : palette.labelFill}
              >
                Request →
              </text>

              {/* Response label */}
              <text
                x={RES_X}
                y={22}
                textAnchor="middle"
                fontSize={10}
                fontWeight={600}
                fontFamily="monospace"
                fill={isResPhase || isAllActive ? palette.activeEdgeStroke : palette.labelFill}
              >
                ← Response
              </text>

              {/* Request arrows (left side, downward) */}
              {LAYERS.map((_, i) => {
                const topY = i === 0 ? 32 : getLayerY(i - 1) + LAYER_H / 2;
                const botY = getLayerY(i) - LAYER_H / 2;
                const active = reqArrowActive(i);
                return (
                  <motion.line
                    key={`req-${i}`}
                    x1={REQ_X}
                    y1={topY}
                    x2={REQ_X}
                    y2={botY}
                    strokeWidth={active ? 2.5 : 1.5}
                    markerEnd={active ? "url(#pipe-arrow-down-active)" : "url(#pipe-arrow-down)"}
                    animate={{ stroke: arrowColor(active), strokeWidth: active ? 2.5 : 1.5 }}
                    transition={{ duration: 0.4 }}
                  />
                );
              })}

              {/* Response arrows (right side, upward) */}
              {LAYERS.map((_, i) => {
                const topY = i === 0 ? 32 : getLayerY(i - 1) + LAYER_H / 2;
                const botY = getLayerY(i) - LAYER_H / 2;
                const active = resArrowActive(i);
                return (
                  <motion.line
                    key={`res-${i}`}
                    x1={RES_X}
                    y1={botY}
                    x2={RES_X}
                    y2={topY}
                    strokeWidth={active ? 2.5 : 1.5}
                    markerEnd={active ? "url(#pipe-arrow-up-active)" : "url(#pipe-arrow-up)"}
                    animate={{ stroke: arrowColor(active), strokeWidth: active ? 2.5 : 1.5 }}
                    transition={{ duration: 0.4 }}
                  />
                );
              })}

              {/* Pipeline layers */}
              {LAYERS.map((layer, i) => {
                const cy = getLayerY(i);
                const active = isLayerActive(i);
                const isLast = i === LAYERS.length - 1;

                return (
                  <g key={layer.id}>
                    {/* Layer box */}
                    <motion.rect
                      x={CENTER_X - LAYER_W / 2}
                      y={cy - LAYER_H / 2}
                      width={LAYER_W}
                      height={LAYER_H}
                      rx={10}
                      strokeWidth={2}
                      animate={{
                        fill: active ? palette.activeNodeFill : palette.nodeFill,
                        stroke: active ? palette.activeNodeStroke : palette.nodeStroke,
                      }}
                      filter={active ? glowFilter(layer) : "none"}
                      transition={{ duration: 0.4 }}
                    />
                    {/* Layer name */}
                    <motion.text
                      x={CENTER_X}
                      y={cy - 10}
                      textAnchor="middle"
                      dominantBaseline="middle"
                      fontSize={13}
                      fontWeight={700}
                      fontFamily="monospace"
                      animate={{ fill: active ? palette.activeNodeText : palette.nodeText }}
                      transition={{ duration: 0.4 }}
                    >
                      {layer.name}
                    </motion.text>
                    {/* Pre / Post actions */}
                    <motion.text
                      x={CENTER_X}
                      y={cy + 8}
                      textAnchor="middle"
                      dominantBaseline="middle"
                      fontSize={9}
                      fontFamily="monospace"
                      animate={{ fill: active ? "rgba(255,255,255,0.7)" : palette.labelFill }}
                      transition={{ duration: 0.4 }}
                    >
                      {isLast
                        ? `${layer.preAction}  |  ${layer.postAction}`
                        : `pre: ${layer.preAction}  |  post: ${layer.postAction}`}
                    </motion.text>
                    {/* "DelegatingChatClient" label on left */}
                    <motion.text
                      x={CENTER_X - LAYER_W / 2 - 8}
                      y={cy}
                      textAnchor="end"
                      dominantBaseline="middle"
                      fontSize={7}
                      fontFamily="monospace"
                      animate={{ fill: active ? palette.activeNodeText : palette.labelFill }}
                      transition={{ duration: 0.4 }}
                    >
                      {isLast ? "" : "DelegatingChatClient"}
                    </motion.text>
                  </g>
                );
              })}

              {/* Annotation for current step */}
              {(isReqPhase || isResPhase) && activeLayerIdx >= 0 && (
                <motion.g
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  transition={{ duration: 0.3 }}
                >
                  <rect
                    x={CENTER_X + LAYER_W / 2 + 14}
                    y={getLayerY(activeLayerIdx) - 14}
                    width={120}
                    height={28}
                    rx={6}
                    fill={
                      LAYERS[activeLayerIdx].id === "timing"
                        ? "#f97316"
                        : LAYERS[activeLayerIdx].id === "logging"
                          ? "#0ea5e9"
                          : "#8b5cf6"
                    }
                    opacity={0.9}
                  />
                  <text
                    x={CENTER_X + LAYER_W / 2 + 74}
                    y={getLayerY(activeLayerIdx) + 1}
                    textAnchor="middle"
                    dominantBaseline="middle"
                    fontSize={9}
                    fontWeight={600}
                    fontFamily="monospace"
                    fill="#ffffff"
                  >
                    {isReqPhase ? LAYERS[activeLayerIdx].preAction : LAYERS[activeLayerIdx].postAction}
                  </text>
                </motion.g>
              )}
            </svg>
          </div>

          {/* Right panel: flow summary (35%) */}
          <div className="w-full lg:w-[35%]">
            <div className="mb-2 font-mono text-xs text-zinc-400 dark:text-zinc-500">
              Pipeline flow
            </div>
            <div className="space-y-1.5 rounded-md border border-zinc-100 bg-zinc-50 p-3 dark:border-zinc-800 dark:bg-zinc-950">
              {LAYERS.map((layer, i) => {
                const active = isLayerActive(i);
                const bgColors = [
                  "bg-orange-100 dark:bg-orange-950/40",
                  "bg-sky-100 dark:bg-sky-950/40",
                  "bg-violet-100 dark:bg-violet-950/40",
                ];
                const borderColors = [
                  "border-orange-300 dark:border-orange-800",
                  "border-sky-300 dark:border-sky-800",
                  "border-violet-300 dark:border-violet-800",
                ];
                const textColors = [
                  "text-orange-800 dark:text-orange-200",
                  "text-sky-800 dark:text-sky-200",
                  "text-violet-800 dark:text-violet-200",
                ];

                return (
                  <motion.div
                    key={layer.id}
                    initial={false}
                    animate={{
                      opacity: active ? 1 : 0.5,
                      scale: active ? 1 : 0.98,
                    }}
                    transition={{ duration: 0.3 }}
                    className={`rounded-md border px-3 py-2 ${bgColors[i]} ${borderColors[i]}`}
                  >
                    <div className={`font-mono text-xs font-semibold ${textColors[i]}`}>
                      {layer.name}
                    </div>
                    <div className="mt-0.5 font-mono text-[10px] text-zinc-500 dark:text-zinc-400">
                      pre: {layer.preAction}
                    </div>
                    <div className="font-mono text-[10px] text-zinc-500 dark:text-zinc-400">
                      post: {layer.postAction}
                    </div>
                  </motion.div>
                );
              })}

              {/* Arrow direction indicator */}
              <div className="mt-3 border-t border-zinc-200 pt-2 dark:border-zinc-700">
                <div className="flex items-center gap-2 text-[10px] text-zinc-400 dark:text-zinc-500">
                  <span>Request ↓</span>
                  <span className="text-zinc-300 dark:text-zinc-600">|</span>
                  <span>Response ↑</span>
                </div>
              </div>
            </div>
          </div>
        </div>
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
