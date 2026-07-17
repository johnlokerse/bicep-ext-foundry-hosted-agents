import asyncio
import os
from contextlib import suppress
from typing import Protocol
from urllib.parse import urlparse

from azure.ai.projects.aio import AIProjectClient
from azure.ai.agentserver.responses import (
    CreateResponse,
    ResponseContext,
    ResponsesAgentServerHost,
    TextResponse,
)
from azure.core.exceptions import ClientAuthenticationError
from azure.identity.aio import DefaultAzureCredential
from openai import (
    APIConnectionError,
    APIError,
    APIStatusError,
    AsyncOpenAI,
    AuthenticationError,
)


SYSTEM_INSTRUCTIONS = (
    "You are a helpful, reliable general-purpose assistant. "
    "Answer clearly, accurately, and concisely."
)


class ConfigurationError(RuntimeError):
    pass


class ModelAuthenticationError(RuntimeError):
    pass


class ModelCallError(RuntimeError):
    pass


class ModelClient(Protocol):
    async def generate(self, user_input: str) -> str: ...


class FoundryModelClient:
    def __init__(
        self,
        openai_client: AsyncOpenAI,
        model_deployment_name: str,
        *,
        credential: DefaultAzureCredential | None = None,
        project_client: AIProjectClient | None = None,
    ) -> None:
        self._openai_client = openai_client
        self._model_deployment_name = model_deployment_name
        self._credential = credential
        self._project_client = project_client

    @classmethod
    def from_environment(cls) -> "FoundryModelClient":
        endpoint = _required_environment_variable("FOUNDRY_PROJECT_ENDPOINT").rstrip("/")
        parsed_endpoint = urlparse(endpoint)
        if (
            parsed_endpoint.scheme != "https"
            or not parsed_endpoint.netloc
            or "/api/projects/" not in parsed_endpoint.path
        ):
            raise ConfigurationError(
                "FOUNDRY_PROJECT_ENDPOINT must be an HTTPS Microsoft Foundry project "
                "endpoint containing '/api/projects/'."
            )

        model_deployment_name = _required_environment_variable("MODEL_DEPLOYMENT_NAME")
        credential = DefaultAzureCredential()
        project_client = AIProjectClient(endpoint=endpoint, credential=credential)
        openai_client = project_client.get_openai_client()

        return cls(
            openai_client,
            model_deployment_name,
            credential=credential,
            project_client=project_client,
        )

    async def generate(self, user_input: str) -> str:
        try:
            response = await self._openai_client.responses.create(
                model=self._model_deployment_name,
                instructions=SYSTEM_INSTRUCTIONS,
                input=user_input,
            )
        except (ClientAuthenticationError, AuthenticationError) as error:
            raise ModelAuthenticationError(
                "Microsoft Foundry authentication failed. Verify that the hosted "
                "agent identity can access the project and model deployment."
            ) from error
        except APIConnectionError as error:
            raise ModelCallError(
                "Unable to connect to the Microsoft Foundry model endpoint."
            ) from error
        except APIStatusError as error:
            raise ModelCallError(
                "Microsoft Foundry model call failed with HTTP "
                f"{error.status_code}: {error.message}"
            ) from error
        except APIError as error:
            raise ModelCallError(
                f"Microsoft Foundry model call failed: {error}"
            ) from error

        output_text = response.output_text
        if not output_text or not output_text.strip():
            raise ModelCallError(
                "Microsoft Foundry returned a model response without output text."
            )

        return output_text


_model_client: ModelClient | None = None


def _required_environment_variable(name: str) -> str:
    value = os.getenv(name)
    if value is None or not value.strip():
        raise ConfigurationError(
            f"Required environment variable {name} is not configured."
        )
    return value.strip()


def get_model_client() -> ModelClient:
    global _model_client
    if _model_client is None:
        _model_client = FoundryModelClient.from_environment()
    return _model_client


async def generate_with_cancellation(
    model_client: ModelClient,
    user_input: str,
    cancellation_signal: asyncio.Event,
) -> str:
    model_task = asyncio.create_task(model_client.generate(user_input))
    cancellation_task = asyncio.create_task(cancellation_signal.wait())

    try:
        done, _ = await asyncio.wait(
            (model_task, cancellation_task),
            return_when=asyncio.FIRST_COMPLETED,
        )
        if cancellation_task in done and cancellation_signal.is_set():
            raise asyncio.CancelledError(
                "The hosted agent request was cancelled before the model call completed."
            )
        return await model_task
    except asyncio.CancelledError:
        model_task.cancel()
        with suppress(asyncio.CancelledError):
            await model_task
        raise
    finally:
        cancellation_task.cancel()
        with suppress(asyncio.CancelledError):
            await cancellation_task


async def handler(
    request: CreateResponse,
    context: ResponseContext,
    cancellation_signal: asyncio.Event,
) -> TextResponse:
    user_input = await context.get_input_text()
    if not user_input or not user_input.strip():
        raise ValueError("The response input does not contain user text.")

    generated_text = await generate_with_cancellation(
        get_model_client(),
        user_input,
        cancellation_signal,
    )
    return TextResponse(context, request, text=generated_text)


def create_server() -> ResponsesAgentServerHost:
    server = ResponsesAgentServerHost()
    server.response_handler(handler)
    return server


if __name__ == "__main__":
    create_server().run()
