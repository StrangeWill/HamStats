<template>
  <v-container>
    <v-card flat>
      <v-card-title>
        Radios
      </v-card-title>
      <v-card-text>
        <v-data-table :headers="radioHeaders" :items-per-page="-1" :items="radios" hide-default-footer>
          <template v-slot:item.vfOs="{ item }">
            <div v-for="vfo in item.vfOs">
              {{ vfo.name }} : {{ formatFrequency(vfo.txFrequency) }} / {{ formatFrequency(vfo.txFrequency) }}
            </div>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>
    <v-card flat>
      <v-card-title>
        Contacts
      </v-card-title>
      <v-card-text>
        <v-data-table :headers="contactHeaders" :items-per-page="-1" :items="contacts" hide-default-footer>
          <template v-slot:item.date="{ item }">
            {{ formatDateTime(item.date) }}
          </template>
          <template v-slot:item.txFrequency="{ item }">
            {{ formatFrequency(item.txFrequency) }}
          </template>
          <template v-slot:item.rxFrequency="{ item }">
            {{ formatFrequency(item.rxFrequency) }}
          </template>
          <template v-slot:item.gridsquare="{ item }">
            {{ item.gridsquare ?? "—" }}
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>
    <v-card flat>
      <v-card-title>
        Scores
      </v-card-title>
      <v-card-text>
        <v-data-table :headers="scoreHeaders" :items-per-page="-1" :items="scores.breakdown" hide-default-footer>
        </v-data-table>
      </v-card-text>
    </v-card>
  </v-container>
</template>

<script setup lang="ts">
import axios from "axios";
import { ref, onMounted, onUnmounted } from "vue";
import { HubConnectionBuilder, HubConnection } from "@microsoft/signalr";
import { formatDateTime } from "@/timezone";

const contacts = ref<any[]>([]);
const radios = ref<any[]>([]);
const scores = ref<any>({});

const radioHeaders = [
  { key: "name", title: "Radio Name" },
  { key: "operator", title: "Operator" },
  { key: "vfOs", title: "VFOs" },
  { key: "contacts", title: "Contacts" },
  { key: "rate10", title: "Last 10" },
  { key: "rate100", title: "Last 100" },
  { key: "last15m", title: "15m Qs" },
  { key: "last60m", title: "60m Qs" }
];

const contactHeaders = [
  { key: "radio", title: "Radio" },
  { key: "operator", title: "Operator" },
  { key: "date", title: "Date" },
  { key: "toCall", title: "toCall" },
  { key: "mode", title: "Mode" },
  { key: "band", title: "Band" },
  { key: "rxFrequency", title: "Rx Frequency" },
  { key: "txFrequency", title: "Tx Frequency" },
  { key: "class", title: "Class" },
  { key: "section", title: "Section" },
  { key: "gridsquare", title: "Grid" },
];

const scoreHeaders = [
  { key: "band", title: "Band" },
  { key: "mode", title: "Mode" },
  { key: "points", title: "Points" },
  { key: "qsOs", title: "QSOs" },
];

// ChatGPT-generated frequency formatter — hence the mess.
function formatFrequency(number: string | number) {
  let numStr = number.toString();
  numStr = numStr.padStart(6, '0');
  let formatted;
  if (numStr.length <= 6) {
    formatted = numStr.slice(0, 1) + '.' + numStr.slice(1, 4) + '.' + numStr.slice(4, 6);
  } else if (numStr.length <= 7) {
    formatted = numStr.slice(0, 2) + '.' + numStr.slice(2, 5) + '.' + numStr.slice(5, 7);
  } else {
    formatted = numStr.slice(0, numStr.length - 6) + '.' +
      numStr.slice(numStr.length - 6, numStr.length - 3) + '.' +
      numStr.slice(numStr.length - 3);
  }
  formatted += 'mhz';
  return formatted;
}

async function fetchContacts() {
  contacts.value = (await axios.get("/api/v0/contacts")).data;
}
async function fetchRadios() {
  radios.value = (await axios.get("/api/v0/radios")).data;
}
async function fetchScores() {
  scores.value = (await axios.get("/api/v0/scores")).data;
}
function fetchAll() {
  return Promise.all([fetchContacts(), fetchRadios(), fetchScores()]);
}

function refetch(dataset: string) {
  if (dataset === "contacts") return fetchContacts();
  if (dataset === "radios") return fetchRadios();
  if (dataset === "scores") return fetchScores();
}

// Coalesce nudges: a burst of QSOs becomes one refetch per dataset, not one per packet.
const dirty = new Set<string>();
let flushTimer: number | undefined;
function scheduleFlush(datasets: string[]) {
  datasets.forEach((d) => dirty.add(d));
  if (flushTimer !== undefined) return;
  flushTimer = window.setTimeout(() => {
    flushTimer = undefined;
    const pending = [...dirty];
    dirty.clear();
    // Run rates live in the radios payload, so a contact change should refresh radios too.
    if (pending.includes("contacts") && !pending.includes("radios")) pending.push("radios");
    pending.forEach(refetch);
  }, 300);
}

let connection: HubConnection | null = null;
let rateTimer: number | undefined; // refresh run rates so they decay over time, not just on new QSOs

onMounted(() => {
  fetchAll(); // initial state via REST
  rateTimer = window.setInterval(fetchRadios, 12000);
  connection = new HubConnectionBuilder()
    .withUrl("/hubs/dashboard")
    .withAutomaticReconnect()
    .build();
  connection.on("DataChanged", (datasets: string[]) => scheduleFlush(datasets));
  // After a dropped connection, refetch everything to catch up on anything missed.
  connection.onreconnected(() => fetchAll());
  connection.start().catch(() => {});
});

onUnmounted(() => {
  if (flushTimer !== undefined) clearTimeout(flushTimer);
  if (rateTimer !== undefined) clearInterval(rateTimer);
  connection?.stop();
});
</script>
