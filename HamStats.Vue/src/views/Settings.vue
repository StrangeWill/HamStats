<template>
  <v-container style="max-width: 700px">
    <v-card flat class="mb-4">
      <v-card-title>Display</v-card-title>
      <v-card-text>
        <v-select
          v-model="selected"
          :items="zones"
          item-title="label"
          item-value="id"
          label="Local time zone"
          variant="outlined"
          hide-details
          :loading="saving"
          @update:model-value="save"
        />
        <div class="text-caption mt-2 mb-4 text-medium-emphasis">
          All times on the dashboard are shown in this zone. Now: {{ preview }}
        </div>
        <v-text-field
          v-model.number="cycle"
          type="number"
          label="Auto-cycle dwell (seconds)"
          variant="outlined"
          hide-details
          min="5"
          max="600"
          :loading="savingCycle"
          @change="saveCycle"
        />
        <div class="text-caption mt-2 text-medium-emphasis">
          How long each view stays up before the kiosk rotates to the next (5–600s). {{ cycleMessage }}
        </div>
      </v-card-text>
    </v-card>

    <v-card flat class="mb-4">
      <v-card-title>Station Location</v-card-title>
      <v-card-text>
        <v-text-field
          v-model="grid"
          label="Our grid square (Maidenhead)"
          placeholder="e.g. FN31pr"
          variant="outlined"
          :rules="[gridRule]"
          :loading="savingGrid"
          @keyup.enter="saveGrid"
        >
          <template #append>
            <v-btn color="primary" :disabled="gridRule(grid) !== true" :loading="savingGrid" @click="saveGrid">
              Save
            </v-btn>
          </template>
        </v-text-field>
        <div class="text-caption text-medium-emphasis">
          The origin point for contact lines on the map. {{ gridMessage }}
        </div>
      </v-card-text>
    </v-card>

    <v-card flat>
      <v-card-title>Callsign grid database</v-card-title>
      <v-card-text>
        <div class="text-body-2 mb-3">
          Offline callsign → grid lookup used to fill in grids when N1MM doesn't send one.
          <template v-if="status">
            <strong>{{ status.count.toLocaleString() }}</strong> callsigns,
            last built {{ status.updatedAt ? formatDateTime(status.updatedAt) : "never" }}.
          </template>
        </div>
        <div class="d-flex ga-2">
          <v-btn color="primary" :loading="importing" @click="runImport">Refresh now</v-btn>
          <!-- Hangfire is server-rendered, so a plain navigation (not a router link). -->
          <v-btn variant="outlined" href="/hangfire" target="_blank" rel="noopener">
            Open Hangfire (job dashboard)
          </v-btn>
        </div>
        <div v-if="importMessage" class="text-caption mt-2 text-medium-emphasis">{{ importMessage }}</div>
      </v-card-text>
    </v-card>

    <!-- Live, per-run import log via the Asio job-session SignalR stream (scoped to this job's session). -->
    <v-dialog v-model="showLog" max-width="1100" scrollable>
      <JobSessionLog
        title="Callsign Import Log"
        :session-id="sessionId"
        @close="showLog = false"
        @complete="loadStatus"
      />
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import axios from "axios";
import { ref, computed, onMounted, onUnmounted } from "vue";
import { timeZone, setTimeZone, formatDateTime } from "@/timezone";
import { stationGrid, setStationGrid } from "@/station";
import { cycleSeconds, setCycleSeconds } from "@/kiosk";
import JobSessionLog from "@c/JobSessionLog.vue";

interface Zone {
  id: string;
  label: string;
}

interface CallsignStatus {
  count: number;
  updatedAt: string | null;
}

const zones = ref<Zone[]>([]);
const selected = ref<string>(timeZone.value);
const saving = ref(false);
const nowIso = ref<string>(new Date().toISOString());

const grid = ref<string>(stationGrid.value ?? "");
const savingGrid = ref(false);
const gridMessage = ref("");
const gridRule = (v: string) =>
  /^[A-R]{2}[0-9]{2}([a-x]{2})?$/i.test(v ?? "") || "Enter a valid 4- or 6-character grid square.";

const cycle = ref<number>(cycleSeconds.value);
const savingCycle = ref(false);
const cycleMessage = ref("");

const status = ref<CallsignStatus | null>(null);
const importing = ref(false);
const importMessage = ref("");
const showLog = ref(false);
const sessionId = ref<string | null>(null);

const preview = computed(() => formatDateTime(nowIso.value));

async function save(value: string) {
  saving.value = true;
  try {
    await axios.put("/api/v0/settings", { timeZone: value });
    setTimeZone(value);
  } finally {
    saving.value = false;
  }
}

async function saveGrid() {
  if (gridRule(grid.value) !== true) return;
  savingGrid.value = true;
  gridMessage.value = "";
  try {
    await axios.put("/api/v0/settings/grid", { grid: grid.value });
    setStationGrid(grid.value);
    gridMessage.value = "Saved.";
  } catch {
    gridMessage.value = "Failed to save grid.";
  } finally {
    savingGrid.value = false;
  }
}

async function saveCycle() {
  const seconds = Number(cycle.value);
  if (!Number.isFinite(seconds) || seconds < 5 || seconds > 600) {
    cycleMessage.value = "Enter 5–600 seconds.";
    return;
  }
  savingCycle.value = true;
  cycleMessage.value = "";
  try {
    await axios.put("/api/v0/settings/cycle", { seconds });
    setCycleSeconds(seconds);
    cycleMessage.value = "Saved.";
  } catch {
    cycleMessage.value = "Failed to save.";
  } finally {
    savingCycle.value = false;
  }
}

async function loadStatus() {
  const { data } = await axios.get("/api/v0/callsigns/status");
  status.value = data;
}

async function runImport() {
  importing.value = true;
  importMessage.value = "";
  try {
    const { data } = await axios.post("/api/v0/callsigns/import");
    importMessage.value = "Import queued — counts update here once it finishes.";
    // Open the live log scoped to this run's session.
    sessionId.value = data.sessionId;
    showLog.value = true;
  } catch {
    importMessage.value = "Failed to queue import.";
  } finally {
    importing.value = false;
  }
}

let clock: number | undefined;
let statusTimer: number | undefined;

onMounted(async () => {
  const { data } = await axios.get("/api/v0/settings/timezones");
  zones.value = data;
  selected.value = timeZone.value;
  grid.value = stationGrid.value ?? "";
  cycle.value = cycleSeconds.value;
  await loadStatus();
  // keep the preview clock ticking, and refresh the build status periodically
  clock = setInterval(() => (nowIso.value = new Date().toISOString()), 1000);
  statusTimer = setInterval(loadStatus, 5000);
});

onUnmounted(() => {
  clearInterval(clock);
  clearInterval(statusTimer);
});
</script>
