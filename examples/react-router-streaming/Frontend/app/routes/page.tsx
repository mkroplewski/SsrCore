import { Suspense } from "react";
import type { Route } from "./+types/page";
import { Await } from "react-router";

export function loader({ context }: Route.LoaderArgs) {
  return { data: context.greeterService.greetAsync("World") };
}

export default function Page({ loaderData }: Route.ComponentProps) {
  return (
    <div>
      <Suspense fallback={<p className="text-center text-gray-900 dark:text-gray-100">Loading greeting...</p>}>
        <Await resolve={loaderData.data}>{(greeting) => <p className="text-center text-gray-900 dark:text-gray-100">Greeting: {greeting}</p>}</Await>
      </Suspense>
    </div>
  );
}
