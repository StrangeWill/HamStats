import { ref } from "vue";
import axios from "axios";

// App-wide display time zone, loaded from the server settings. All times in the UI are rendered
// in this zone instead of UTC. Server stores times as UTC (ISO with 'Z'); we convert on display.
export const timeZone = ref<string>("America/New_York");

export async function loadTimeZone(): Promise<void> {
  try {
    const { data } = await axios.get("/api/v0/settings");
    if (data?.timeZone) {
      timeZone.value = data.timeZone;
    }
  } catch {
    // keep default
  }
}

export function setTimeZone(value: string): void {
  timeZone.value = value;
}

function format(iso: string | null | undefined, options: Intl.DateTimeFormatOptions): string {
  if (!iso) return "";
  const date = new Date(iso);
  if (isNaN(date.getTime())) return String(iso);
  return new Intl.DateTimeFormat(undefined, { timeZone: timeZone.value, hour12: false, ...options }).format(date);
}

export function formatDateTime(iso: string | null | undefined): string {
  return format(iso, {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

export function formatTime(iso: string | null | undefined): string {
  return format(iso, { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}
