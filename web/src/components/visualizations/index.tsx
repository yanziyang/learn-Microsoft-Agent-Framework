"use client";

import { lazy, Suspense } from "react";
import { useTranslations } from "@/lib/i18n";

const visualizations: Record<
  string,
  React.LazyExoticComponent<React.ComponentType<{ title?: string }>>
> = {
  s01: lazy(() => import("./s01-provider-switching")),
  s02: lazy(() => import("./s02-middleware-pipeline")),
  s03: lazy(() => import("./s03-agent-loop")),
  s04: lazy(() => import("./s04-tool-dispatch")),
  s05: lazy(() => import("./s05-permission")),
  s06: lazy(() => import("./s06-hooks")),
  s07: lazy(() => import("./s07-todo-write")),
  s08: lazy(() => import("./s08-subagent")),
  s09: lazy(() => import("./s09-skill-loading")),
  s10: lazy(() => import("./s10-context-compact")),
  s11: lazy(() => import("./s11-system-prompt")),
  s12: lazy(() => import("./s12-error-recovery")),
  s13: lazy(() => import("./s13-task-system")),
  s14: lazy(() => import("./s14-background-tasks")),
  s15: lazy(() => import("./s15-agent-teams")),
  s16: lazy(() => import("./s16-team-protocols")),
  s17: lazy(() => import("./s17-mcp-tools")),
  s18: lazy(() => import("./s20-comprehensive")),
  s19: lazy(() => import("./s20-comprehensive")),
  s20: lazy(() => import("./s20-comprehensive")),
};

export function SessionVisualization({ version }: { version: string }) {
  const t = useTranslations("viz");
  const Component = visualizations[version];
  if (!Component) return null;
  return (
    <Suspense
      fallback={
        <div className="min-h-[500px] animate-pulse rounded-lg bg-zinc-100 dark:bg-zinc-800" />
      }
    >
      <div className="min-h-[500px]">
        <Component title={t(version)} />
      </div>
    </Suspense>
  );
}
