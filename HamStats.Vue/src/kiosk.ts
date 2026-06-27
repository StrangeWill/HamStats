import { ref } from "vue";
import axios from "axios";

// How long each view stays up before the kiosk auto-cycle advances, in seconds. Loaded from
// server settings so it's shared across every display; defaults to 60s.
export const cycleSeconds = ref<number>(60);

export async function loadCycleSeconds(): Promise<void> {
  try {
    const { data } = await axios.get("/api/v0/settings");
    if (typeof data?.cycleSeconds === "number") {
      cycleSeconds.value = data.cycleSeconds;
    }
  } catch {
    // keep default
  }
}

export function setCycleSeconds(value: number): void {
  cycleSeconds.value = value;
}
