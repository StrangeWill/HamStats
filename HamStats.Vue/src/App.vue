<template>
  <v-app>
    <v-app-bar title="W3DEV Field Day Stats">
      <template #append>
        <v-btn variant="text" :to="{ name: 'dashboard' }">Dashboard</v-btn>
        <v-btn variant="text" :to="{ name: 'map' }">Map</v-btn>
        <v-btn variant="text" :to="{ name: 'chat' }">Chat</v-btn>
        <v-btn variant="text" :to="{ name: 'settings' }">Settings</v-btn>
        <v-btn variant="text" @click="showLog = true">Packet Log</v-btn>
        <v-btn variant="text" :color="cycling ? 'primary' : undefined" @click="cycling = !cycling">
          {{ cycling ? "Auto ▶" : "Auto ❚❚" }}
        </v-btn>
      </template>
    </v-app-bar>
    <v-main>
      <router-view />
    </v-main>

    <v-dialog v-model="showLog" max-width="1100" scrollable>
      <LogConsole @close="showLog = false" />
    </v-dialog>
  </v-app>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted } from "vue";
import { useRouter } from "vue-router";
import LogConsole from "@c/LogConsole.vue";
import { loadTimeZone } from "@/timezone";
import { loadStationGrid } from "@/station";
import { cycleSeconds, loadCycleSeconds } from "@/kiosk";

const showLog = ref(false);
const router = useRouter();

// Kiosk auto-cycle: rotate through the display views hands-free. Pauses while the Packet Log
// dialog is open, while parked on an admin route (e.g. Settings), or when toggled off.
const cycleRoutes = ["map", "dashboard"];
const cycling = ref(false);

function stepCycle() {
  if (!cycling.value || showLog.value) return;
  const idx = cycleRoutes.indexOf(router.currentRoute.value.name as string);
  if (idx === -1) return; // off on a non-cycle route — don't yank the operator away
  router.push({ name: cycleRoutes[(idx + 1) % cycleRoutes.length] });
}

// Dwell is server-configurable; re-arm the timer whenever it changes.
let cycleTimer: number | undefined;
function armTimer() {
  if (cycleTimer) clearInterval(cycleTimer);
  cycleTimer = window.setInterval(stepCycle, Math.max(5, cycleSeconds.value) * 1000);
}

onMounted(() => {
  loadTimeZone();
  loadStationGrid();
  loadCycleSeconds();
  armTimer();
});

watch(cycleSeconds, armTimer);
onUnmounted(() => clearInterval(cycleTimer));
</script>
