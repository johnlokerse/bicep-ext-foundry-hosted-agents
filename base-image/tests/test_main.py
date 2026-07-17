import asyncio
import os
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, Mock, patch, sentinel

import httpx
from azure.core.exceptions import ClientAuthenticationError
from openai import APIConnectionError


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import main  # noqa: E402


class FoundryModelClientTests(unittest.IsolatedAsyncioTestCase):
    async def test_generate_calls_configured_model(self) -> None:
        create = AsyncMock(
            return_value=SimpleNamespace(output_text="Model-generated answer")
        )
        openai_client = SimpleNamespace(
            responses=SimpleNamespace(create=create)
        )
        client = main.FoundryModelClient(openai_client, "gpt-5.6-luna")

        result = await client.generate("Hello")

        self.assertEqual("Model-generated answer", result)
        create.assert_awaited_once_with(
            model="gpt-5.6-luna",
            instructions=main.SYSTEM_INSTRUCTIONS,
            input="Hello",
        )

    async def test_generate_surfaces_authentication_failure(self) -> None:
        create = AsyncMock(
            side_effect=ClientAuthenticationError(message="identity unavailable")
        )
        client = main.FoundryModelClient(
            SimpleNamespace(responses=SimpleNamespace(create=create)),
            "gpt-5.6-luna",
        )

        with self.assertRaisesRegex(
            main.ModelAuthenticationError,
            "hosted agent identity",
        ):
            await client.generate("Hello")

    async def test_generate_surfaces_connection_failure(self) -> None:
        create = AsyncMock(
            side_effect=APIConnectionError(
                request=httpx.Request("POST", "https://example.test")
            )
        )
        client = main.FoundryModelClient(
            SimpleNamespace(responses=SimpleNamespace(create=create)),
            "gpt-5.6-luna",
        )

        with self.assertRaisesRegex(
            main.ModelCallError,
            "Unable to connect",
        ):
            await client.generate("Hello")

    async def test_generate_rejects_empty_model_output(self) -> None:
        create = AsyncMock(return_value=SimpleNamespace(output_text="  "))
        client = main.FoundryModelClient(
            SimpleNamespace(responses=SimpleNamespace(create=create)),
            "gpt-5.6-luna",
        )

        with self.assertRaisesRegex(main.ModelCallError, "without output text"):
            await client.generate("Hello")


class ConfigurationTests(unittest.TestCase):
    def tearDown(self) -> None:
        main._model_client = None

    def test_missing_project_endpoint_is_clear(self) -> None:
        with patch.dict(
            os.environ,
            {"MODEL_DEPLOYMENT_NAME": "gpt-5.6-luna"},
            clear=True,
        ):
            with self.assertRaisesRegex(
                main.ConfigurationError,
                "FOUNDRY_PROJECT_ENDPOINT",
            ):
                main.FoundryModelClient.from_environment()

    def test_missing_model_deployment_is_clear(self) -> None:
        with patch.dict(
            os.environ,
            {
                "FOUNDRY_PROJECT_ENDPOINT": (
                    "https://example.services.ai.azure.com/api/projects/example"
                )
            },
            clear=True,
        ):
            with self.assertRaisesRegex(
                main.ConfigurationError,
                "MODEL_DEPLOYMENT_NAME",
            ):
                main.FoundryModelClient.from_environment()

    def test_factory_uses_default_credential_and_project_endpoint(self) -> None:
        credential = Mock()
        project_client = Mock()
        openai_client = Mock()
        project_client.get_openai_client.return_value = openai_client

        with (
            patch.dict(
                os.environ,
                {
                    "FOUNDRY_PROJECT_ENDPOINT": (
                        "https://example.services.ai.azure.com/api/projects/example"
                    ),
                    "MODEL_DEPLOYMENT_NAME": "gpt-5.6-luna",
                },
                clear=True,
            ),
            patch.object(
                main,
                "DefaultAzureCredential",
                return_value=credential,
            ) as credential_type,
            patch.object(
                main,
                "AIProjectClient",
                return_value=project_client,
            ) as project_type,
        ):
            client = main.FoundryModelClient.from_environment()

        credential_type.assert_called_once_with()
        project_type.assert_called_once_with(
            endpoint="https://example.services.ai.azure.com/api/projects/example",
            credential=credential,
        )
        project_client.get_openai_client.assert_called_once_with()
        self.assertIs(openai_client, client._openai_client)


class HandlerTests(unittest.IsolatedAsyncioTestCase):
    def tearDown(self) -> None:
        main._model_client = None

    async def test_handler_returns_generated_text_response(self) -> None:
        context = SimpleNamespace(get_input_text=AsyncMock(return_value="Hi"))
        model_client = SimpleNamespace(
            generate=AsyncMock(return_value="Hello from the model")
        )
        cancellation_signal = asyncio.Event()

        with (
            patch.object(main, "get_model_client", return_value=model_client),
            patch.object(
                main,
                "TextResponse",
                return_value=sentinel.response,
            ) as text_response,
        ):
            result = await main.handler(
                sentinel.request,
                context,
                cancellation_signal,
            )

        self.assertIs(sentinel.response, result)
        context.get_input_text.assert_awaited_once_with()
        model_client.generate.assert_awaited_once_with("Hi")
        text_response.assert_called_once_with(
            context,
            sentinel.request,
            text="Hello from the model",
        )

    async def test_cancellation_stops_in_flight_model_call(self) -> None:
        started = asyncio.Event()
        cancelled = asyncio.Event()

        class BlockingModelClient:
            async def generate(self, user_input: str) -> str:
                started.set()
                try:
                    await asyncio.Event().wait()
                except asyncio.CancelledError:
                    cancelled.set()
                    raise

        cancellation_signal = asyncio.Event()
        task = asyncio.create_task(
            main.generate_with_cancellation(
                BlockingModelClient(),
                "Hello",
                cancellation_signal,
            )
        )
        await started.wait()
        cancellation_signal.set()

        with self.assertRaises(asyncio.CancelledError):
            await task
        self.assertTrue(cancelled.is_set())

    async def test_handler_rejects_empty_user_input(self) -> None:
        context = SimpleNamespace(get_input_text=AsyncMock(return_value=" "))

        with self.assertRaisesRegex(ValueError, "does not contain user text"):
            await main.handler(
                sentinel.request,
                context,
                asyncio.Event(),
            )
