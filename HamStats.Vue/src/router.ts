import { createRouter, createWebHistory } from "vue-router";
import Dashboard from "@v/Dashboard.vue";
import Settings from "@v/Settings.vue";
import MapView from "@v/MapView.vue";
import Chat from "@v/Chat.vue";

const router = createRouter({
    history: createWebHistory(),
    routes: [
        // The live map is the default kiosk view; the table scoreboard moves to /dashboard.
        { path: "/", name: "map", component: MapView },
        { path: "/dashboard", name: "dashboard", component: Dashboard },
        { path: "/map", redirect: "/" },
        { path: "/chat", name: "chat", component: Chat },
        { path: "/settings", name: "settings", component: Settings },
    ],
});

export default router;
