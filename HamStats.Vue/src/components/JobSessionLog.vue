<template>
  <v-card flat>
    <v-card-title class="d-flex align-center">
      {{ title }}
      <v-chip :color="chipColor" size="small" class="ml-3" label>{{ statusText }}</v-chip>
      <v-spacer />
      <v-btn size="small" variant="text" @click="$emit('close')">Close</v-btn>
    </v-card-title>
    <v-card-text>
      <div ref="logEl" class="log-console">
        <div v-for="(entry, i) in entries" :key="i" class="log-line">
          <span class="log-level" :style="{ color: levelColor(entry.level) }">{{ levelName(entry.level) }}</span>
          <span class="log-msg">{{ entry.message }}</span>
        </div>
        <div v-if="!entries.length" class="log-empty">Waiting for the job to start…</div>
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, nextTick, watch } from "vue";
import { HubConnectionBuilder, HubConnection, LogLevel } from "@microsoft/signalr";

const props = withDefaults(defineProps<{ sessionId: string | null; title?: string }>(), {
  title: "Job Log",
});
const emit = defineEmits<{ close: []; complete: [hasError: boolean] }>();

interface Entry {
  message: string;
  level: number;
}

const entries = ref<Entry[]>([]);
const connected = ref(false);
const finished = ref<null | { hasError: boolean }>(null);
const logEl = ref<HTMLElement | null>(null);
let connection: HubConnection | null = null;

// Matches Microsoft.Extensions.Logging.LogLevel (Trace=0 … Critical=5).
const LEVELS = ["Trace", "Debug", "Information", "Warning", "Error", "Critical"];
function levelName(level: number) {
  return LEVELS[level] ?? String(level);
}
function levelColor(level: number): string {
  switch (level) {
    case 5:
    case 4:
      return "#ff5252";
    case 3:
      return "#fb8c00";
    case 2:
      return "#42a5f5";
    case 1:
      return "#9e9e9e";
    default:
      return "#616161"; // Trace
  }
}

const statusText = computed(() => {
  if (finished.value) return finished.value.hasError ? "failed" : "complete";
  return connected.value ? "running" : "connecting";
});
const chipColor = computed(() => {
  if (finished.value) return finished.value.hasError ? "red" : "green";
  return connected.value ? "blue" : "grey";
});

async function append(entry: Entry) {
  entries.value.push(entry);
  await nextTick();
  if (logEl.value) logEl.value.scrollTop = logEl.value.scrollHeight;
}

async function watchSession() {
  if (!connection || !props.sessionId) return;
  // Re-watch replays the full history, so reset to avoid duplicates.
  entries.value = [];
  finished.value = null;
  try {
    await connection.invoke("Watch", props.sessionId);
  } catch {
    /* unknown/denied session id returns silently — leave the empty state */
  }
}

onMounted(() => {
  connection = new HubConnectionBuilder()
    .withUrl("/hubs/job-session")
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  connection.on("LogMessage", (message: string, level: number) => append({ message, level }));
  connection.on("SessionComplete", (hasError: boolean) => {
    finished.value = { hasError };
    emit("complete", hasError);
  });
  connection.onreconnected(() => {
    connected.value = true;
    watchSession();
  });
  connection.onreconnecting(() => (connected.value = false));
  connection.onclose(() => (connected.value = false));

  connection
    .start()
    .then(() => {
      connected.value = true;
      return watchSession();
    })
    .catch(() => (connected.value = false));
});

// A new import reuses this component with a fresh session id.
watch(() => props.sessionId, watchSession);

onBeforeUnmount(() => connection?.stop());
</script>

<style scoped>
.log-console {
  height: 360px;
  overflow-y: auto;
  background: #1e1e1e;
  color: #d4d4d4;
  font-family: ui-monospace, "SFMono-Regular", Menlo, Consolas, monospace;
  font-size: 12px;
  line-height: 1.5;
  padding: 8px;
  border-radius: 4px;
  white-space: pre-wrap;
  word-break: break-all;
}
.log-line {
  display: flex;
  gap: 8px;
}
.log-level {
  flex: 0 0 90px;
  font-weight: 600;
}
.log-msg {
  flex: 1 1 auto;
}
.log-empty {
  color: #888;
  font-style: italic;
}
</style>
