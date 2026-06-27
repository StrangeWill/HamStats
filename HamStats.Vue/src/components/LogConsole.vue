<template>
  <v-card flat>
    <v-card-title class="d-flex align-center">
      {{ title }}
      <v-chip :color="connected ? 'green' : 'red'" size="small" class="ml-3" label>
        {{ connected ? "connected" : "disconnected" }}
      </v-chip>
      <v-spacer />
      <v-select
        v-model="level"
        :items="levels"
        label="Capture level"
        density="compact"
        hide-details
        variant="outlined"
        style="max-width: 200px"
        class="mr-3"
        @update:model-value="setLevel"
      />
      <v-btn size="small" variant="text" @click="entries = []">Clear</v-btn>
      <v-btn size="small" variant="text" @click="$emit('close')">Close</v-btn>
    </v-card-title>
    <v-card-text>
      <div ref="logEl" class="log-console">
        <div v-for="entry in entries" :key="entry.sequence" class="log-line">
          <span class="log-time">{{ formatTime(entry.timestampUtc) }}</span>
          <span class="log-level" :style="{ color: levelColor(entry.level) }">{{ entry.level }}</span>
          <span class="log-msg">{{ entry.message }}</span>
        </div>
        <div v-if="!entries.length" class="log-empty">
          Waiting for log output… (raise the capture level to see more)
        </div>
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import axios from "axios";
import { ref, onMounted, onBeforeUnmount, nextTick } from "vue";
import { HubConnectionBuilder, HubConnection, LogLevel } from "@microsoft/signalr";
import { formatTime } from "@/timezone";

defineEmits<{ close: [] }>();

withDefaults(defineProps<{ title?: string }>(), { title: "N1MM Packet Log" });

// Matches RoushTech.Asio.AppLogEntry
interface AppLogEntry {
  sequence: number;
  timestampUtc: string;
  level: string;
  category: string;
  message: string;
  exception: string | null;
}

const MAX_LINES = 1000;

const entries = ref<AppLogEntry[]>([]);
const connected = ref(false);
const logEl = ref<HTMLElement | null>(null);
let connection: HubConnection | null = null;

// Runtime capture-level override, applied server-side to the watcher category.
const levels = ["Trace", "Debug", "Information", "Warning", "Error"];
const level = ref<string>("Information");

async function loadLevel() {
  const { data } = await axios.get("/api/v0/loglevel");
  level.value = data.level;
}

async function setLevel(value: string) {
  await axios.put("/api/v0/loglevel", { level: value });
}

function levelColor(level: string): string {
  switch (level) {
    case "Critical":
    case "Error":
      return "#ff5252";
    case "Warning":
      return "#fb8c00";
    case "Information":
      return "#42a5f5";
    case "Debug":
      return "#9e9e9e";
    default:
      return "#616161"; // Trace
  }
}

async function addEntry(entry: AppLogEntry) {
  // Hub streams backlog then live entries, already sequence-ordered.
  entries.value.push(entry);
  if (entries.value.length > MAX_LINES) {
    entries.value.splice(0, entries.value.length - MAX_LINES);
  }
  await nextTick();
  if (logEl.value) {
    logEl.value.scrollTop = logEl.value.scrollHeight;
  }
}

onMounted(() => {
  loadLevel();
  connection = new HubConnectionBuilder()
    .withUrl("/hubs/app-log")
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  connection.on("AppLog", (entry: AppLogEntry) => addEntry(entry));
  connection.onreconnected(() => (connected.value = true));
  connection.onreconnecting(() => (connected.value = false));
  connection.onclose(() => (connected.value = false));

  connection
    .start()
    .then(() => (connected.value = true))
    .catch(() => (connected.value = false));
});

onBeforeUnmount(() => {
  connection?.stop();
});
</script>

<style scoped>
.log-console {
  height: 300px;
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
.log-time {
  color: #6a9955;
  flex: 0 0 auto;
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
