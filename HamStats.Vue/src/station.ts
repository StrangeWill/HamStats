import { ref } from "vue";
import axios from "axios";

// Our station's Maidenhead grid, loaded from server settings. Origin point for every QSO arc on
// the map; null until an operator sets it in Settings.
export const stationGrid = ref<string | null>(null);

export async function loadStationGrid(): Promise<void> {
  try {
    const { data } = await axios.get("/api/v0/settings");
    stationGrid.value = data?.stationGrid ?? null;
  } catch {
    // keep current value
  }
}

export function setStationGrid(value: string): void {
  stationGrid.value = value;
}
