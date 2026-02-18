// SmartTrafficMonitor - site.js

(function () {
  function onReady(fn) {
    if (document.readyState === "loading") {
      document.addEventListener("DOMContentLoaded", fn);
    } else {
      fn();
    }
  }

  function initHeatmapIframeControls() {
    var form = document.getElementById("heatmapForm");
    var frame = document.getElementById("heatmapFrame");
    if (!form || !frame) return;

    form.addEventListener("submit", function (e) {
      e.preventDefault();

      var zoneEl = document.getElementById("HeatmapZone");
      var periodEl = document.getElementById("HeatmapPeriod");
      if (!zoneEl || !periodEl) return;

      var zone = zoneEl.value;
      var period = periodEl.value;

      var params = new URLSearchParams({ zone: zone, period: period }).toString();
      frame.src = "/Heatmap/View?" + params;
    });
  }

  // HeatmapView.cshtml
  function initLeafletHeatmapPage() {
    var mapEl = document.getElementById("map");
    if (!mapEl) return;

    // Leaflet must exist 
    if (typeof window.L === "undefined") return;

    var centerLat = parseFloat(mapEl.getAttribute("data-center-lat") || "-37.7985");
    var centerLng = parseFloat(mapEl.getAttribute("data-center-lng") || "144.9015");

    var pointsRaw = mapEl.getAttribute("data-heat-points") || "[]";
    var heatPoints;
    try {
      heatPoints = JSON.parse(pointsRaw);
    } catch (e) {
      heatPoints = [];
    }

    var map = window.L.map("map", { zoomControl: true }).setView([centerLat, centerLng], 15);

    window.L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap contributors",
    }).addTo(map);

    if (window.L.heatLayer) {
      window.L.heatLayer(heatPoints, {
        radius: 30,
        blur: 22,
        maxZoom: 17,
      }).addTo(map);
    }
  }

  onReady(function () {
    initHeatmapIframeControls();
    initLeafletHeatmapPage();
  });
})();
