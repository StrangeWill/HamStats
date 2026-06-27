<template>
  <v-container class="chat-wrap">
    <v-card class="chat-card" flat>
      <v-card-title class="d-flex align-center ga-3">
        <span>Chat</span>
        <v-spacer />
        <v-select
          v-model="me"
          :items="radioNames"
          label="I am"
          density="compact"
          variant="outlined"
          hide-details
          style="max-width: 220px"
        />
      </v-card-title>

      <v-card-text class="messages" ref="scroller">
        <div v-for="m in messages" :key="m.id" class="msg" :class="{ mine: m.radio === me }">
          <div class="meta">
            <span class="from">{{ m.radio }}</span>
            <span v-if="m.operator" class="op">· {{ m.operator }}</span>
            <span class="time">{{ formatTime(m.date) }}</span>
          </div>
          <div class="text">{{ m.text }}</div>
        </div>
        <div v-if="!messages.length" class="empty">No messages yet.</div>
      </v-card-text>

      <v-card-actions class="composer">
        <v-text-field
          v-model="draft"
          :disabled="!me"
          :placeholder="me ? 'Message…' : 'Select your radio first'"
          density="compact"
          variant="outlined"
          hide-details
          maxlength="500"
          @keyup.enter="send"
        />
        <v-btn color="primary" :disabled="!me || !draft.trim()" :loading="sending" @click="send">Send</v-btn>
      </v-card-actions>
    </v-card>
  </v-container>
</template>

<script setup lang="ts">
import axios from "axios";
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from "vue";
import { HubConnectionBuilder, HubConnection } from "@microsoft/signalr";
import { formatTime } from "@/timezone";

interface ChatMessage {
  id: string;
  date: string;
  radio: string;
  operator: string | null;
  text: string;
}

const radios = ref<any[]>([]);
const messages = ref<ChatMessage[]>([]);
const draft = ref("");
const sending = ref(false);
const scroller = ref<{ $el: HTMLElement } | null>(null);

// "Which radio am I" is a per-device choice, kept in localStorage so it sticks across reloads.
const me = ref<string>(localStorage.getItem("chatRadio") ?? "");
watch(me, (v) => localStorage.setItem("chatRadio", v ?? ""));

const radioNames = computed(() => radios.value.map((r) => r.name));

function scrollToBottom() {
  nextTick(() => {
    const el = scroller.value?.$el;
    if (el) el.scrollTop = el.scrollHeight;
  });
}

async function fetchRadios() {
  radios.value = (await axios.get("/api/v0/radios")).data;
}
async function fetchMessages() {
  const atBottom = isAtBottom();
  messages.value = (await axios.get("/api/v0/messages")).data;
  if (atBottom) scrollToBottom();
}

function isAtBottom(): boolean {
  const el = scroller.value?.$el;
  if (!el) return true;
  return el.scrollHeight - el.scrollTop - el.clientHeight < 60;
}

async function send() {
  const text = draft.value.trim();
  if (!text || !me.value || sending.value) return;
  sending.value = true;
  try {
    const operator = radios.value.find((r) => r.name === me.value)?.operator ?? null;
    await axios.post("/api/v0/messages", { radio: me.value, operator, text });
    draft.value = "";
    await fetchMessages();
    scrollToBottom();
  } finally {
    sending.value = false;
  }
}

let connection: HubConnection | null = null;

onMounted(async () => {
  await Promise.all([fetchRadios(), fetchMessages()]);
  scrollToBottom();

  connection = new HubConnectionBuilder().withUrl("/hubs/dashboard").withAutomaticReconnect().build();
  connection.on("DataChanged", (datasets: string[]) => {
    if (datasets.includes("messages")) fetchMessages();
    if (datasets.includes("radios")) fetchRadios();
  });
  connection.onreconnected(() => {
    fetchRadios();
    fetchMessages();
  });
  connection.start().catch(() => {});
});

onUnmounted(() => connection?.stop());
</script>

<style scoped>
.chat-wrap {
  height: calc(100dvh - 64px);
  display: flex;
}
.chat-card {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}
.messages {
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}
.msg {
  padding: 4px 0;
}
.msg.mine .text {
  color: rgb(var(--v-theme-primary));
}
.meta {
  display: flex;
  align-items: baseline;
  gap: 6px;
  font-size: 12px;
}
.from {
  font-weight: 700;
}
.op {
  opacity: 0.7;
}
.time {
  margin-left: auto;
  opacity: 0.55;
  font-variant-numeric: tabular-nums;
}
.text {
  white-space: pre-wrap;
  word-break: break-word;
}
.empty {
  opacity: 0.6;
  padding: 16px 0;
}
.composer {
  gap: 8px;
}
</style>
