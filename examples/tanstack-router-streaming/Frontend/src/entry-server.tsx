import { createRequestHandler, defaultStreamHandler } from "@tanstack/react-router/ssr/server";
import { createRouter } from "./router";
import { Services } from "./services";
export default async function render(request: Request, services: Services) {
  const handler = createRequestHandler({ request, createRouter: () => createRouter(services) });
  return await handler(defaultStreamHandler);
}
