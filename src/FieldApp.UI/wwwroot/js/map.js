// Leaflet maps (plan 7.10). Leaflet (~150 KB) is loaded on first use, not at app start. Markers are
// circle/div markers, so none of Leaflet's image assets are needed. Without a connection the tiles simply
// don't load — markers, the rep's position and the geofence still render on a blank background.

const LEAFLET = './_content/PharmaERP.FieldApp.UI/lib/leaflet/';
let leafletReady = null;

function loadLeaflet() {
  if (window.L) return Promise.resolve(window.L);
  if (leafletReady) return leafletReady;
  leafletReady = new Promise((resolve, reject) => {
    const css = document.createElement('link');
    css.rel = 'stylesheet';
    css.href = LEAFLET + 'leaflet.css';
    document.head.appendChild(css);
    const script = document.createElement('script');
    script.src = LEAFLET + 'leaflet.js';
    script.onload = () => resolve(window.L);
    script.onerror = () => { leafletReady = null; reject(new Error('Leaflet failed to load')); };
    document.head.appendChild(script);
  });
  return leafletReady;
}

const maps = new Map();
let nextId = 1;

export async function create(element, options) {
  const L = await loadLeaflet();
  const map = L.map(element, { zoomControl: true, attributionControl: true })
    .setView([options.latitude, options.longitude], options.zoom ?? 15);
  L.tileLayer(options.tileUrl, { maxZoom: 19, attribution: options.attribution }).addTo(map);
  const id = nextId++;
  maps.set(id, { map, markers: L.layerGroup().addTo(map), self: null, fence: null });
  // The container may have been sized after creation (tabs, lazy layout).
  setTimeout(() => { if (maps.has(id)) map.invalidateSize(); }, 0);
  return id;
}

export function setMarkers(id, markers, fit) {
  const m = maps.get(id);
  if (!m) return;
  m.markers.clearLayers();
  const L = window.L;
  const points = [];
  for (const mk of markers) {
    const icon = L.divIcon({
      className: 'map-pin',
      html: `<span class="map-pin-dot" style="background:${mk.color}">${mk.label ?? ''}</span>`,
      iconSize: [28, 28],
      iconAnchor: [14, 14]
    });
    const marker = L.marker([mk.latitude, mk.longitude], { icon, title: mk.title ?? '' });
    if (mk.title) marker.bindPopup(mk.title);
    m.markers.addLayer(marker);
    points.push([mk.latitude, mk.longitude]);
  }
  if (fit && points.length > 0) fitTo(m, points);
}

export function setSelf(id, self) {
  const m = maps.get(id);
  if (!m) return;
  const L = window.L;
  if (m.self) { m.self.forEach(layer => layer.remove()); m.self = null; }
  if (!self) return;
  const at = [self.latitude, self.longitude];
  m.self = [
    L.circle(at, { radius: self.accuracyMeters, color: '#1a73e8', weight: 1, fillOpacity: 0.12 }).addTo(m.map),
    L.circleMarker(at, { radius: 7, color: '#fff', weight: 2, fillColor: '#1a73e8', fillOpacity: 1 }).addTo(m.map)
  ];
}

export function setGeofence(id, fence) {
  const m = maps.get(id);
  if (!m) return;
  if (m.fence) { m.fence.remove(); m.fence = null; }
  if (!fence) return;
  m.fence = window.L.circle([fence.latitude, fence.longitude],
    { radius: fence.radiusMeters, color: fence.color ?? '#1e8e3e', weight: 2, dashArray: '6 6', fillOpacity: 0.05 })
    .addTo(m.map);
}

export function fitAll(id) {
  const m = maps.get(id);
  if (!m) return;
  const points = [];
  m.markers.eachLayer(l => points.push(l.getLatLng()));
  if (m.self) points.push(m.self[1].getLatLng());
  if (m.fence) points.push(m.fence.getLatLng());
  if (points.length) fitTo(m, points);
}

// No animation: a screen can be left mid-zoom (check-out navigates straight away), and Leaflet's
// zoom-end handler then runs against a removed map ("_leaflet_pos" of undefined).
function fitTo(m, points) {
  if (points.length === 1) m.map.setView(points[0], Math.max(m.map.getZoom(), 16), { animate: false });
  else m.map.fitBounds(window.L.latLngBounds(points), { padding: [32, 32], maxZoom: 17, animate: false });
}

export function dispose(id) {
  const m = maps.get(id);
  if (!m) return;
  m.map.stop();
  m.map.remove();
  maps.delete(id);
}
