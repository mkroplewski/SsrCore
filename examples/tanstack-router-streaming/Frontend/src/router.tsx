import { createRouter as createReactRouter } from "@tanstack/react-router";

import { routeTree } from "./routeTree.gen";
import { Services } from "./services";

export function createRouter(services: Services) {
  return createReactRouter({
    routeTree,
    context: {
      head: "",
      services: services,
    },
    defaultPreload: "intent",
    scrollRestoration: true,
  });
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof createRouter>;
  }
}
