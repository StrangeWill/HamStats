// Inverse of the server-side Maidenhead.Encode (HamStats.Website/CallsignLookup/Maidenhead.cs):
// decode a 4- or 6-character locator to its centre [lon, lat] (GeoJSON order, ready for d3-geo).
// Returns null for anything that isn't a well-formed grid.
const GRID = /^[A-R]{2}[0-9]{2}([a-x]{2})?$/i;

export function gridToLonLat(grid: string | null | undefined): [number, number] | null {
  if (!grid || !GRID.test(grid)) return null;
  const g = grid.toUpperCase();

  // Field (20° lon / 10° lat) + square (2° / 1°), then centre within the smallest cell we have.
  let lon = (g.charCodeAt(0) - 65) * 20 + Number(g[2]) * 2;
  let lat = (g.charCodeAt(1) - 65) * 10 + Number(g[3]) * 1;

  if (g.length >= 6) {
    // Subsquare: 5' lon / 2.5' lat, then centre of the subsquare.
    lon += (g.charCodeAt(4) - 65) * (2 / 24) + 1 / 24;
    lat += (g.charCodeAt(5) - 65) * (1 / 24) + 0.5 / 24;
  } else {
    lon += 1; // centre of the 2° square
    lat += 0.5; // centre of the 1° square
  }

  return [lon - 180, lat - 90];
}

// Initial-bearing (degrees, 0 = north) from one [lon, lat] to another — used to throw an arc
// toward the map edge for stations that fall outside the projection (Canada/DX).
export function bearing(from: [number, number], to: [number, number]): number {
  const toRad = (d: number) => (d * Math.PI) / 180;
  const [lon1, lat1] = from.map(toRad);
  const [lon2, lat2] = to.map(toRad);
  const dLon = lon2 - lon1;
  const y = Math.sin(dLon) * Math.cos(lat2);
  const x = Math.cos(lat1) * Math.sin(lat2) - Math.sin(lat1) * Math.cos(lat2) * Math.cos(dLon);
  return (Math.atan2(y, x) * 180) / Math.PI;
}
