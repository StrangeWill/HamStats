<template>
  <div ref="wrap" class="map-wrap">
    <svg :viewBox="`0 0 ${W} ${H}`" preserveAspectRatio="xMidYMid meet" class="map-svg">
      <defs>
        <clipPath v-for="win in windows" :key="`clip-${win.id}`" :id="`clip-${win.id}`">
          <rect :x="win.rect.x" :y="win.rect.y" :width="win.rect.w" :height="win.rect.h" />
        </clipPath>
      </defs>

      <!-- Every region — the US, AK, HI, and each DX country — is a window onto the one global
           geographic state. A window projects + clips the world's land, the worked dots, and the
           QSO great-circle lines into its own rectangle. Layout scales with the container aspect. -->
      <g v-for="win in windows" :key="win.id">
        <rect
          v-if="win.frame"
          :x="win.rect.x"
          :y="win.rect.y"
          :width="win.rect.w"
          :height="win.rect.h"
          class="win-bg"
        />
        <g :clip-path="`url(#clip-${win.id})`">
          <path v-if="win.contextPath" :d="win.contextPath" class="land-context" />
          <path v-if="win.landPath" :d="win.landPath" class="land" />
          <circle v-for="(d, i) in win.dots" :key="`d${i}`" :cx="d.x" :cy="d.y" r="2.5" class="dot" :fill="d.color" />
          <circle v-if="win.id === 'us' && home" :cx="home.x" :cy="home.y" r="5" class="home" />
          <g v-for="a in win.arcs" :key="a.id" :style="{ opacity: opacityOf(a) }">
            <path v-if="a.path" :d="a.path" class="arc" :stroke="a.color" />
            <circle v-if="a.dot" :cx="a.dot.x" :cy="a.dot.y" r="3.5" class="arc-end" :fill="a.color" />
            <path v-if="a.arrow" :d="a.arrow" :fill="a.color" />
          </g>
        </g>

        <!-- In-map contact labels: drawn outside the clip so edge labels stay readable. -->
        <template v-for="a in win.arcs" :key="`lbl-${a.id}`">
          <g
            v-if="a.label1 && a.dot"
            :style="{ opacity: opacityOf(a) }"
            :transform="`translate(${a.dot.x + 6}, ${a.dot.y - 6})`"
          >
            <text class="arc-label">{{ a.label1 }}</text>
            <text class="arc-label arc-label-sub" y="12">{{ a.label2 }}</text>
          </g>
        </template>
        <rect
          v-if="win.frame"
          :x="win.rect.x"
          :y="win.rect.y"
          :width="win.rect.w"
          :height="win.rect.h"
          class="win-frame"
        />
        <text v-if="win.label" :x="win.rect.x + 5" :y="win.rect.y + 13" class="win-tag">{{ win.label }}</text>
        <text v-if="win.sub" :x="win.rect.x + 5" :y="win.rect.y + win.rect.h - 17" class="win-sub">{{ win.sub }}</text>
        <text v-if="win.sub2" :x="win.rect.x + 5" :y="win.rect.y + win.rect.h - 5" class="win-sub win-sub2">
          {{ win.sub2 }}
        </text>
      </g>
    </svg>

    <div ref="panelEl" class="radios-panel">
      <div class="panel-title">
        <span>Radios Online</span>
        <span v-if="radios.length" class="rate-legend" title="Last-10 / last-100 QSO rate · 15m / 60m QSOs">
          <span class="rate-n">10</span>
          <span class="rate-n">100</span>
          <span class="rate-n rate-gap">15m</span>
          <span class="rate-n">60m</span>
        </span>
      </div>
      <div v-for="r in radios" :key="r.id" class="radio-row">
        <span class="swatch" :style="{ background: colorFor(r.name) }"></span>
        <div class="radio-main">
          <div class="radio-head">
            <span class="radio-name">{{ r.name }}</span>
            <span class="radio-op">{{ r.operator || "—" }}</span>
            <span class="radio-rate" title="Last-10 / last-100 QSO rate (per hour) · QSOs in the last 15 / 60 min">
              <span class="rate-n">{{ r.rate10 ?? "—" }}</span>
              <span class="rate-n">{{ r.rate100 ?? "—" }}</span>
              <span class="rate-n rate-gap">{{ r.last15m ?? 0 }}</span>
              <span class="rate-n">{{ r.last60m ?? 0 }}</span>
            </span>
          </div>
          <div v-if="r.vfOs?.length" class="radio-freqs">
            <span v-for="v in r.vfOs" :key="v.id" class="vfo">
              <span class="vfo-slice">{{ v.name }}</span> {{ freqShort(v.rxFrequency || v.txFrequency) }}
            </span>
          </div>
        </div>
      </div>
      <div v-if="!radios.length" class="radio-empty">No radios reporting</div>
    </div>

    <!-- Per-band QSO / score breakdown across the top. -->
    <div v-if="bandBreakdown.length" class="score-bar">
      <div v-for="b in bandBreakdown" :key="b.band" class="score-cell">
        <div class="band-name">{{ b.band }}</div>
        <div class="band-qsos">{{ b.qsos }}</div>
        <div class="band-pts">{{ b.points }} pts</div>
      </div>
      <div class="score-cell score-total">
        <div class="band-name">Total</div>
        <div class="band-qsos">{{ totalQsos }}</div>
        <div class="band-pts">{{ totalScore }} pts</div>
      </div>
    </div>

    <!-- Chat ticker: pops up near the bottom for a bit whenever a message arrives. -->
    <transition name="chat-pop">
      <div v-if="chatVisible && chatLog.length" ref="chatScroller" class="chat-overlay">
        <div v-for="m in chatLog" :key="m.id" class="chat-line">
          <span class="chat-time">{{ formatTime(m.date) }}</span>
          <span class="chat-from" :style="{ color: colorFor(m.radio) }">{{ m.operator || m.radio }}</span>
          <span class="chat-text">{{ m.text }}</span>
        </div>
      </div>
    </transition>

    <div v-if="!home" class="overlay">
      <v-card class="pa-4" color="surface">
        <div class="text-h6 mb-1">Set your grid square</div>
        <div class="text-body-2 text-medium-emphasis">
          Add your station's Maidenhead grid in Settings to plot contacts.
        </div>
        <v-btn class="mt-3" color="primary" :to="{ name: 'settings' }">Open Settings</v-btn>
      </v-card>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted, onUnmounted } from "vue";
import axios from "axios";
import { geoAlbers, geoConicEqualArea, geoMercator, geoPath, geoContains, geoBounds, geoInterpolate } from "d3-geo";
import { feature } from "topojson-client";
import { HubConnectionBuilder, HubConnection } from "@microsoft/signalr";
import statesTopo from "us-atlas/states-10m.json";
import countriesTopo from "world-atlas/countries-50m.json";
import { stationGrid, loadStationGrid } from "@/station";
import { gridToLonLat, bearing } from "@/maidenhead";
import { formatTime } from "@/timezone";

type LL = [number, number];
interface Rect2 {
  x: number;
  y: number;
  w: number;
  h: number;
}

// The viewBox height is fixed; its WIDTH tracks the container's aspect ratio so the map always fills
// the screen (no letterboxing). The lower-48 fills a centre band with a GUTTER column on each side
// for DX pop-out windows.
const H = 600;
const GUTTER = 195;
const DX_W = 180;
const DX_H = 120;
const FADE_MS = 60000; // QSO line lifetime

const wrap = ref<HTMLElement>();
const panelEl = ref<HTMLElement>(); // radios panel, measured so right-gutter DX boxes clear it
const aspect = ref(16 / 9); // container width / height, measured at runtime
const containerPxH = ref(1);
const panelPxH = ref(0);
const W = computed(() => Math.max(2 * GUTTER + 380, Math.round(H * aspect.value)));
const usRect = computed<Rect2>(() => ({ x: GUTTER, y: 0, w: W.value - 2 * GUTTER, h: H }));
// Top of the right gutter, pushed below the radios panel (panel px → viewBox units).
const rightGutterTop = computed(() => {
  const px2vb = H / Math.max(1, containerPxH.value);
  return Math.min(H - DX_H - 12, Math.max(12, (panelPxH.value + 26) * px2vb));
});

// --- Geography ----------------------------------------------------------------------------------
const states = (feature(statesTopo as any, (statesTopo as any).objects.states) as any).features as any[];
const countries = (feature(countriesTopo as any, (countriesTopo as any).objects.countries) as any).features as any[];
const AK_ID = "02";
const HI_ID = "15";
// FIPS > 56 are Pacific/Caribbean territories (Guam at 144°E, Samoa at −14°) — excluding them keeps
// the fit on the actual lower-48 instead of stretching across the whole Pacific.
const lower48 = {
  type: "FeatureCollection",
  features: states.filter((f) => +f.id <= 56 && f.id !== AK_ID && f.id !== HI_ID),
};
const akFeature = states.find((f) => f.id === AK_ID);
const hiFeature = states.find((f) => f.id === HI_ID);
function context(names: string[]) {
  return { type: "FeatureCollection", features: countries.filter((f) => names.includes(f.properties?.name)) };
}
function countryAt(lon: number, lat: number): any | null {
  return countries.find((f) => geoContains(f, [lon, lat])) ?? null;
}
function rectExtent(r: Rect2, padTop = 8): [[number, number], [number, number]] {
  return [
    [r.x + 8, r.y + padTop],
    [r.x + r.w - 8, r.y + r.h - 8],
  ];
}

// --- Projections --------------------------------------------------------------------------------
// The US projection (and its land/context paths) depend on W, so they re-fit on resize.
const usProjection = computed(() =>
  geoAlbers().fitExtent(
    [
      [GUTTER + 10, 10],
      [W.value - GUTTER - 10, H - 10],
    ],
    lower48 as any,
  ),
);
const usLand = computed(() => geoPath(usProjection.value)(lower48 as any) ?? "");
const usContext = computed(() => geoPath(usProjection.value)(context(["Canada", "Mexico", "Cuba", "Bahamas"]) as any) ?? "");

// AK/HI windows are pinned bottom-left with their own centred projections (fixed, not W-dependent).
const akRect: Rect2 = { x: 24, y: 415, w: 250, h: 170 };
const akProjection = geoConicEqualArea()
  .rotate([154, 0])
  .center([-2, 58.5])
  .parallels([55, 65])
  .fitExtent(rectExtent(akRect, 20), akFeature);
const akLand = geoPath(akProjection)(akFeature) ?? "";
const akContext = geoPath(akProjection)(context(["Canada", "Russia"]) as any) ?? "";

const hiRect: Rect2 = { x: 290, y: 470, w: 196, h: 115 };
const hiProjection = geoConicEqualArea()
  .rotate([157, 0])
  .center([-3, 19.9])
  .parallels([8, 18])
  .fitExtent(rectExtent(hiRect, 18), hiFeature);
const hiLand = geoPath(hiProjection)(hiFeature) ?? "";

// --- Classification (geographic, so it's stable across resizes) ---------------------------------
function isAK(ll: LL) {
  return ll[1] >= 51 && ll[0] <= -129;
}
function isHI(ll: LL) {
  return ll[1] >= 18 && ll[1] <= 23 && ll[0] >= -161 && ll[0] <= -154;
}
function isNorthAmerica(ll: LL) {
  return ll[0] >= -170 && ll[0] <= -52 && ll[1] >= 12 && ll[1] <= 75;
}
function destOf(ll: LL): "us" | "ak" | "hi" | "dx" {
  if (isAK(ll)) return "ak";
  if (isHI(ll)) return "hi";
  if (isNorthAmerica(ll)) return "us";
  return "dx";
}

// Trim a country to the polygons near the contact, so far-flung overseas territories (e.g. French
// Guiana) don't zoom the inset out to the whole globe.
function localCountry(f: any, ll: LL): any {
  const geom = f.geometry;
  if (geom.type !== "MultiPolygon") return f;
  const polys = geom.coordinates.map((c: any) => ({ type: "Polygon", coordinates: c }));
  if (polys.length === 1) return f;
  const here = polys.find((p: any) => geoContains({ type: "Feature", geometry: p } as any, ll)) ?? polys[0];
  const [[w, s], [e, n]] = geoBounds({ type: "Feature", geometry: here } as any);
  const padLon = e - w + 14;
  const padLat = n - s + 10;
  const near = polys.filter((p: any) => {
    const [[pw, ps], [pe, pn]] = geoBounds({ type: "Feature", geometry: p } as any);
    return pe >= w - padLon && pw <= e + padLon && pn >= s - padLat && ps <= n + padLat;
  });
  return { type: "Feature", properties: f.properties, geometry: { type: "MultiPolygon", coordinates: near.map((p: any) => p.coordinates) } };
}

// --- Line drawing -------------------------------------------------------------------------------
function gcPath(project: (ll: LL) => [number, number] | null, from: LL, to: LL): string {
  const interp = geoInterpolate(from, to);
  const N = 48;
  let d = "";
  for (let i = 0; i <= N; i++) {
    const p = project(interp(i / N) as LL);
    if (!p || !Number.isFinite(p[0]) || !Number.isFinite(p[1])) continue;
    d += (d ? "L" : "M") + p[0].toFixed(1) + "," + p[1].toFixed(1);
  }
  return d;
}
// AK/HI fold back onto the continent under the US projection, so their leaving line can't be a great
// circle there — a true-bearing ray, clipped by SVG to the US rect, heads the right way.
function bearingRay(hx: number, hy: number, from: LL, to: LL, len: number): string {
  const b = (bearing(from, to) * Math.PI) / 180;
  const dx = Math.sin(b);
  const dy = -Math.cos(b);
  return `M${hx},${hy} L${(hx + dx * len).toFixed(1)},${(hy + dy * len).toFixed(1)}`;
}
function arrowAt(project: (ll: LL) => [number, number] | null, from: LL, to: LL): string {
  const interp = geoInterpolate(from, to);
  const tip = project(to);
  const prev = project(interp(0.97) as LL);
  if (!tip || !prev) return "";
  const ang = Math.atan2(tip[1] - prev[1], tip[0] - prev[0]);
  const s = 7;
  const a1 = ang + Math.PI - 0.45;
  const a2 = ang + Math.PI + 0.45;
  return `M${tip[0]},${tip[1]} L${tip[0] + s * Math.cos(a1)},${tip[1] + s * Math.sin(a1)} L${tip[0] + s * Math.cos(a2)},${tip[1] + s * Math.sin(a2)} Z`;
}

// --- Global state (geographic; projection happens reactively in the window views) ----------------
interface GDot {
  ll: LL;
  color: string;
}
interface Arc {
  id: number;
  born: number;
  color: string;
  line1: string;
  line2: string;
  ll: LL;
  region?: any; // trimmed country geometry (DX only)
  name?: string; // country / callsign label (DX only)
}

const pdots = ref<GDot[]>([]); // persistent worked-station markers (US/AK/HI)
const arcs = ref<Arc[]>([]); // live, fading QSO lines
const radios = ref<any[]>([]);
const scoreData = ref<any>({});
const chatLog = ref<{ id: string; date: string; radio: string; operator: string | null; text: string }[]>([]);
const chatVisible = ref(false);
const chatScroller = ref<HTMLElement>();
const now = ref(0);
const seen = new Set<string>();
let nextArcId = 1;

const homeLL = computed(() => gridToLonLat(stationGrid.value) as LL | null);
const home = computed(() => {
  if (!homeLL.value) return null;
  const p = usProjection.value(homeLL.value);
  return p ? { x: p[0], y: p[1] } : null;
});

// Per-band QSO/score totals for the top strip — collapse the per-band/mode breakdown to one row
// per band, busiest first.
const bandBreakdown = computed(() => {
  const map = new Map<string, { band: string; qsos: number; points: number }>();
  for (const b of scoreData.value?.breakdown ?? []) {
    if (!b.band || b.band.toUpperCase() === "TOTAL") continue; // N1MM includes a summary row
    const e = map.get(b.band) ?? { band: b.band, qsos: 0, points: 0 };
    e.qsos += b.qsOs ?? 0;
    e.points += b.points ?? 0;
    map.set(b.band, e);
  }
  return [...map.values()].sort((a, b) => b.qsos - a.qsos);
});
const totalQsos = computed(() => bandBreakdown.value.reduce((s, b) => s + b.qsos, 0));
const totalScore = computed(() => scoreData.value?.value ?? 0);

const PALETTE = ["#42a5f5", "#ef5350", "#66bb6a", "#ffa726", "#ab47bc", "#26c6da", "#d4e157", "#ec407a"];
const radioColors = new Map<string, string>();
function colorFor(name: string | null | undefined): string {
  const key = name || "—";
  if (!radioColors.has(key)) radioColors.set(key, PALETTE[radioColors.size % PALETTE.length]);
  return radioColors.get(key)!;
}

// Hold full opacity for most of the lifetime, then drop off quickly over the final stretch.
function opacityOf(a: { born: number }): number {
  const t = (now.value - a.born) / FADE_MS;
  if (t >= 1) return 0;
  const HOLD = 0.75;
  return t < HOLD ? 1 : (1 - t) / (1 - HOLD);
}

function freqMhz(freq: string | number | null | undefined): string {
  const n = Number(freq);
  return n ? `${(n / 100000).toFixed(3)} MHz` : "";
}
function freqShort(freq: string | number | null | undefined): string {
  const n = Number(freq);
  return n ? `${(n / 100000).toFixed(3)}mhz` : "";
}

// --- Window views -------------------------------------------------------------------------------
interface ArcView {
  id: number;
  born: number;
  color: string;
  path: string;
  dot?: { x: number; y: number };
  arrow?: string;
  label1?: string;
  label2?: string;
}
interface WinView {
  id: string;
  rect: Rect2;
  frame: boolean;
  label?: string;
  sub?: string;
  sub2?: string;
  contextPath: string;
  landPath: string;
  dots: { x: number; y: number; color: string }[];
  arcs: ArcView[];
}

function projectDots(dots: GDot[], project: (ll: LL) => [number, number] | null) {
  const out: { x: number; y: number; color: string }[] = [];
  for (const d of dots) {
    const p = project(d.ll);
    if (p) out.push({ x: p[0], y: p[1], color: d.color });
  }
  return out;
}
// Persistent dots projected per window (depends only on pdots + projection, not on the live arcs).
const usDots = computed(() => projectDots(pdots.value.filter((d) => !isAK(d.ll) && !isHI(d.ll)), usProjection.value));
const akDots = computed(() => projectDots(pdots.value.filter((d) => isAK(d.ll)), akProjection));
const hiDots = computed(() => projectDots(pdots.value.filter((d) => isHI(d.ll)), hiProjection));

const windows = computed<WinView[]>(() => {
  const usP = usProjection.value;
  const hLL = homeLL.value;
  const hS = home.value;
  const w = W.value;

  const usArcs: ArcView[] = [];
  const akArcs: ArcView[] = [];
  const hiArcs: ArcView[] = [];
  const dxArcs: Arc[] = [];

  for (const a of arcs.value) {
    const dest = destOf(a.ll);
    if (dest === "us") {
      const s = usP(a.ll);
      usArcs.push({
        id: a.id,
        born: a.born,
        color: a.color,
        path: hLL ? gcPath((p) => usP(p), hLL, a.ll) : "",
        dot: s ? { x: s[0], y: s[1] } : undefined,
        label1: s ? a.line1 : undefined,
        label2: s ? a.line2 : undefined,
      });
    } else if (dest === "ak" || dest === "hi") {
      // Leaving ray in the US window...
      if (hLL && hS) usArcs.push({ id: a.id, born: a.born, color: a.color, path: bearingRay(hS.x, hS.y, hLL, a.ll, w + H) });
      // ...arriving as a great circle inside the AK/HI window.
      const proj = dest === "ak" ? akProjection : hiProjection;
      const d = proj(a.ll);
      const view: ArcView = {
        id: a.id,
        born: a.born,
        color: a.color,
        path: hLL ? gcPath((p) => proj(p), hLL, a.ll) : "",
        dot: d ? { x: d[0], y: d[1] } : undefined,
        arrow: hLL ? arrowAt((p) => proj(p), hLL, a.ll) : "",
      };
      (dest === "ak" ? akArcs : hiArcs).push(view);
    } else {
      // DX: a real great circle leaving the US window (curves, SVG-clipped); arrival handled below.
      usArcs.push({ id: a.id, born: a.born, color: a.color, path: hLL ? gcPath((p) => usP(p), hLL, a.ll) : "" });
      dxArcs.push(a);
    }
  }

  const list: WinView[] = [
    { id: "us", rect: usRect.value, frame: false, contextPath: usContext.value, landPath: usLand.value, dots: usDots.value, arcs: usArcs },
    { id: "ak", rect: akRect, frame: true, label: "AK", contextPath: akContext, landPath: akLand, dots: akDots.value, arcs: akArcs },
    { id: "hi", rect: hiRect, frame: true, label: "HI", contextPath: "", landPath: hiLand, dots: hiDots.value, arcs: hiArcs },
  ];

  // DX pop-out windows stack in the side gutters; position scales with W.
  let leftN = 0;
  let rightN = 0;
  for (const a of dxArcs) {
    const dLon = hLL ? ((a.ll[0] - hLL[0] + 540) % 360) - 180 : 0;
    const side = dLon >= 0 ? "right" : "left";
    const slot = side === "right" ? rightN++ : leftN++;
    const top = side === "right" ? rightGutterTop.value : 12;
    const rect: Rect2 = { x: side === "right" ? w - 13 - DX_W : 13, y: top + slot * (DX_H + 10), w: DX_W, h: DX_H };
    const region = a.region;
    const proj = region ? geoMercator().fitExtent(rectExtent({ ...rect, h: rect.h - 22 }, 18), region) : null;
    const project = (p: LL) => (proj ? proj(p) : null);
    const d = proj ? proj(a.ll) : null;
    const dot = d ? { x: d[0], y: d[1] } : { x: rect.x + rect.w / 2, y: rect.y + rect.h / 2 };
    list.push({
      id: `dx-${a.id}`,
      rect,
      frame: true,
      label: a.name ?? "DX",
      sub: a.line1,
      sub2: a.line2,
      contextPath: "",
      landPath: proj && region ? geoPath(proj)(region) ?? "" : "",
      dots: [],
      arcs: [{ id: a.id, born: a.born, color: a.color, path: hLL && proj ? gcPath(project, hLL, a.ll) : "", dot, arrow: hLL && proj ? arrowAt(project, hLL, a.ll) : "" }],
    });
  }

  return list;
});

// --- Ingest (stores geographic data only; projection is reactive above) --------------------------
function ingest(contact: any, animate: boolean) {
  if (!contact.id || seen.has(contact.id)) return;
  seen.add(contact.id);

  const grid: string | undefined = contact.gridsquare;
  if (!grid) return;
  const ll = gridToLonLat(grid) as LL | null;
  if (!ll) return;
  const color = colorFor(contact.radio);
  const dest = destOf(ll);

  if (dest !== "dx") pdots.value.push({ ll, color }); // domestic contacts keep a persistent marker

  if (!animate || !homeLL.value) return;

  const arc: Arc = {
    id: nextArcId++,
    born: now.value,
    color,
    line1: `${contact.toCall ?? "?"} · ${contact.mode ?? ""} ${freqMhz(contact.txFrequency)}`.trim(),
    line2: `${contact.radio ?? ""}${contact.operator ? " · " + contact.operator : ""}`,
    ll,
  };
  if (dest === "dx") {
    const country = countryAt(ll[0], ll[1]);
    arc.region = country ? localCountry(country, ll) : null;
    arc.name = country?.properties?.name ?? contact.toCall ?? "DX";
  }
  arcs.value.push(arc);
}

async function fetchRadios() {
  const { data } = await axios.get("/api/v0/radios");
  [...data].sort((a, b) => a.name.localeCompare(b.name)).forEach((r) => colorFor(r.name));
  radios.value = data;
}
async function fetchScores() {
  scoreData.value = (await axios.get("/api/v0/scores")).data ?? {};
}

// Pop the chat ticker with the latest few messages, then auto-hide after a quiet spell.
let chatHideTimer: number | undefined;
async function popChat() {
  try {
    chatLog.value = (await axios.get("/api/v0/messages?take=4")).data;
  } catch {
    return;
  }
  if (!chatLog.value.length) return;
  chatVisible.value = true;
  nextTick(() => {
    const el = chatScroller.value;
    if (el) el.scrollTop = el.scrollHeight; // keep the newest message in view
  });
  if (chatHideTimer !== undefined) clearTimeout(chatHideTimer);
  chatHideTimer = window.setTimeout(() => (chatVisible.value = false), 30000);
}
async function seed() {
  const { data } = await axios.get("/api/v0/contacts");
  (data as any[]).forEach((c) => ingest(c, false));
}
async function pullNew() {
  const { data } = await axios.get("/api/v0/contacts");
  (data as any[]).forEach((c) => ingest(c, true));
}

let raf = 0;
function tick(ts: number) {
  now.value = ts;
  if (arcs.value.length) {
    const keep = arcs.value.filter((a) => ts - a.born < FADE_MS);
    if (keep.length !== arcs.value.length) arcs.value = keep;
  }
  raf = requestAnimationFrame(tick);
}

const dirty = new Set<string>();
let flushTimer: number | undefined;
function scheduleFlush(datasets: string[]) {
  datasets.forEach((d) => dirty.add(d));
  if (flushTimer !== undefined) return;
  flushTimer = window.setTimeout(() => {
    flushTimer = undefined;
    if (dirty.has("contacts")) pullNew();
    // Rates/counts depend on contacts, so refresh the radio panel on either dataset.
    if (dirty.has("contacts") || dirty.has("radios")) fetchRadios();
    if (dirty.has("scores")) fetchScores();
    if (dirty.has("messages")) popChat();
    dirty.clear();
  }, 300);
}

let connection: HubConnection | null = null;
let rateTimer: number | undefined; // refresh run rates so they decay over time, not just on new QSOs
let resizeObs: ResizeObserver | undefined;
let panelObs: ResizeObserver | undefined;

onMounted(async () => {
  now.value = performance.now();
  raf = requestAnimationFrame(tick);
  if (wrap.value) {
    resizeObs = new ResizeObserver((entries) => {
      const r = entries[0].contentRect;
      if (r.height > 0) {
        aspect.value = r.width / r.height;
        containerPxH.value = r.height;
      }
    });
    resizeObs.observe(wrap.value);
  }
  if (panelEl.value) {
    panelObs = new ResizeObserver(() => (panelPxH.value = panelEl.value?.offsetHeight ?? 0));
    panelObs.observe(panelEl.value);
  }
  if (stationGrid.value === null) await loadStationGrid();
  await Promise.all([seed(), fetchRadios(), fetchScores()]);
  rateTimer = window.setInterval(fetchRadios, 12000);

  connection = new HubConnectionBuilder().withUrl("/hubs/dashboard").withAutomaticReconnect().build();
  connection.on("DataChanged", (datasets: string[]) => scheduleFlush(datasets));
  connection.onreconnected(() => {
    pullNew();
    fetchRadios();
  });
  connection.start().catch(() => {});
});

onUnmounted(() => {
  cancelAnimationFrame(raf);
  if (flushTimer !== undefined) clearTimeout(flushTimer);
  if (rateTimer !== undefined) clearInterval(rateTimer);
  if (chatHideTimer !== undefined) clearTimeout(chatHideTimer);
  resizeObs?.disconnect();
  panelObs?.disconnect();
  connection?.stop();
});
</script>

<style scoped>
.map-wrap {
  position: relative;
  height: calc(100dvh - 64px);
  width: 100%;
  background: rgb(var(--v-theme-background));
}
.map-svg {
  width: 100%;
  height: 100%;
}
.land {
  fill: rgba(var(--v-theme-on-surface), 0.06);
  stroke: rgba(var(--v-theme-on-surface), 0.25);
  stroke-width: 0.5;
}
.land-context {
  fill: rgba(var(--v-theme-on-surface), 0.03);
  stroke: rgba(var(--v-theme-on-surface), 0.12);
  stroke-width: 0.4;
}
.dot {
  opacity: 0.55;
}
.home {
  fill: white;
  stroke: rgb(var(--v-theme-primary));
  stroke-width: 2;
}
.arc {
  fill: none;
  stroke-width: 2;
  stroke-linecap: round;
}
.arc-label {
  fill: rgb(var(--v-theme-on-surface));
  font-size: 11px;
  font-weight: 600;
  paint-order: stroke;
  stroke: rgb(var(--v-theme-background));
  stroke-width: 3;
}
.arc-label-sub {
  font-weight: 400;
  opacity: 0.8;
}
.win-bg {
  fill: rgb(var(--v-theme-background));
}
.win-frame {
  fill: none;
  stroke: rgba(var(--v-theme-on-surface), 0.35);
  stroke-width: 1;
  stroke-dasharray: 4 3;
}
.win-tag {
  fill: rgba(var(--v-theme-on-surface), 0.6);
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.04em;
}
.win-sub {
  fill: rgb(var(--v-theme-on-surface));
  font-size: 10px;
  font-weight: 600;
  paint-order: stroke;
  stroke: rgb(var(--v-theme-background));
  stroke-width: 3;
  opacity: 0.85;
}
.win-sub2 {
  font-weight: 400;
  opacity: 0.6;
}
.score-bar {
  position: absolute;
  top: 12px;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  gap: 2px;
  padding: 6px 6px;
  border-radius: 8px;
  background: rgba(var(--v-theme-surface), 0.88);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}
.score-cell {
  text-align: center;
  padding: 2px 12px;
}
.score-total {
  border-left: 1px solid rgba(var(--v-theme-on-surface), 0.18);
}
.band-name {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  opacity: 0.6;
}
.band-qsos {
  font-size: 17px;
  font-weight: 700;
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
}
.band-pts {
  font-size: 11px;
  opacity: 0.65;
  font-variant-numeric: tabular-nums;
}
.radios-panel {
  position: absolute;
  top: 12px;
  right: 12px;
  min-width: 250px;
  padding: 10px 12px;
  border-radius: 8px;
  background: rgba(var(--v-theme-surface), 0.88);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  font-size: 13px;
}
.panel-title {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 16px;
  font-weight: 700;
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  opacity: 0.7;
  margin-bottom: 6px;
}
.rate-legend {
  display: flex;
  text-transform: none;
  letter-spacing: 0;
  font-size: 10px;
  font-weight: 600;
  opacity: 0.7;
}
.radio-row {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 3px 0;
}
.radio-main {
  flex: 1;
  min-width: 0;
}
.radio-head {
  display: flex;
  align-items: baseline;
  gap: 8px;
}
.radio-rate {
  margin-left: auto;
  display: flex;
  font-size: 13px;
  font-variant-numeric: tabular-nums;
  font-weight: 600;
  opacity: 0.85;
}
.rate-n {
  padding: 0 7px;
  border-left: 1px solid rgba(var(--v-theme-on-surface), 0.18);
}
.rate-n:first-child {
  border-left: none;
  padding-left: 0;
}
.rate-n:last-child {
  padding-right: 0;
}
.rate-gap {
  border-left-color: rgba(var(--v-theme-on-surface), 0.45);
}
.swatch {
  width: 12px;
  height: 12px;
  border-radius: 3px;
  flex: none;
}
.radio-name {
  font-weight: 600;
}
.radio-op {
  opacity: 0.75;
}
.radio-freqs {
  display: flex;
  gap: 14px;
  margin-top: 1px;
  font-size: 13px;
  font-variant-numeric: tabular-nums;
  opacity: 0.85;
}
.vfo-slice {
  opacity: 0.5;
  font-weight: 600;
}
.radio-empty {
  opacity: 0.6;
}
.chat-overlay {
  position: absolute;
  bottom: 16px;
  right: 16px;
  max-width: min(520px, 48%);
  max-height: 30vh;
  overflow-y: auto;
  padding: 8px 14px;
  border-radius: 8px;
  background: rgba(var(--v-theme-surface), 0.9);
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.4);
  font-size: 14px;
  pointer-events: none;
}
.chat-line {
  padding: 2px 0;
  line-height: 1.3;
  word-break: break-word;
}
.chat-time {
  opacity: 0.5;
  font-variant-numeric: tabular-nums;
  margin-right: 6px;
}
.chat-from {
  font-weight: 700;
  margin-right: 6px;
}
.chat-pop-enter-active,
.chat-pop-leave-active {
  transition:
    opacity 0.3s ease,
    transform 0.3s ease;
}
.chat-pop-enter-from,
.chat-pop-leave-to {
  opacity: 0;
  transform: translateY(12px);
}
.overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;
}
.overlay :deep(.v-card) {
  pointer-events: auto;
}
</style>
