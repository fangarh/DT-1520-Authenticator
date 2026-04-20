package ru.dt1520.security.authenticator.app.deviceruntime

import com.sun.net.httpserver.HttpServer
import java.net.InetSocketAddress
import java.time.Instant
import java.util.UUID
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class HttpDeviceRuntimeTransportTest {
    @Test
    fun parsesActivationResponseAndSendsIntegrationAuthorizationHeader() = runBlocking {
        var capturedAuthorization: String? = null
        val server = HttpServer.create(InetSocketAddress(0), 0).apply {
            createContext("/api/v1/devices/activate") { exchange ->
                capturedAuthorization = exchange.requestHeaders.getFirst("Authorization")
                val response = """
                    {
                      "device": {
                        "id": "73092591-d55c-49a6-bb50-6d7d64d32499"
                      },
                      "tokens": {
                        "accessToken": "access-token",
                        "refreshToken": "refresh-token",
                        "tokenType": "Bearer",
                        "expiresIn": 900,
                        "scope": "challenge"
                      }
                    }
                """.trimIndent()
                exchange.sendResponseHeaders(201, response.toByteArray().size.toLong())
                exchange.responseBody.use { it.write(response.toByteArray()) }
            }
            start()
        }

        try {
            val transport = HttpDeviceRuntimeTransport("http://127.0.0.1:${server.address.port}")

            val activated = transport.activate(
                command = DeviceActivationCommand(
                    tenantId = UUID.fromString("924cecc1-b0bc-48f1-8502-b5fe5b6ff62f"),
                    externalUserId = "operator@example.local",
                    activationCode = "activation-code",
                    installationId = "installation-1234",
                    deviceName = "Pixel 10 Pro"
                ),
                integrationAccessToken = "integration-access-token"
            )

            assertEquals("Bearer integration-access-token", capturedAuthorization)
            assertEquals(UUID.fromString("73092591-d55c-49a6-bb50-6d7d64d32499"), activated.deviceId)
            assertEquals("access-token", activated.tokens.accessToken)
            assertEquals("refresh-token", activated.tokens.refreshToken)
        } finally {
            server.stop(0)
        }
    }

    @Test
    fun parsesPendingApprovalsFromDeviceEndpoint() = runBlocking {
        val server = HttpServer.create(InetSocketAddress(0), 0).apply {
            createContext("/api/v1/devices/me/challenges/pending") { exchange ->
                val response = """
                    [
                      {
                        "id": "8f536e34-a37a-4f1a-9675-2a0c0e1e2d1a",
                        "factorType": "push",
                        "status": "pending",
                        "operationType": "login",
                        "operationDisplayName": "Approve sign in",
                        "username": "push.api",
                        "expiresAt": "2026-04-17T12:00:00Z",
                        "correlationId": "corr-1"
                      }
                    ]
                """.trimIndent()
                exchange.sendResponseHeaders(200, response.toByteArray().size.toLong())
                exchange.responseBody.use { it.write(response.toByteArray()) }
            }
            start()
        }

        try {
            val transport = HttpDeviceRuntimeTransport("http://127.0.0.1:${server.address.port}")

            val approvals = transport.listPending("Bearer access-token")

            assertEquals(1, approvals.size)
            assertEquals("login", approvals.single().operationType)
            assertEquals("Approve sign in", approvals.single().operationDisplayName)
            assertEquals(Instant.parse("2026-04-17T12:00:00Z"), approvals.single().expiresAt)
        } finally {
            server.stop(0)
        }
    }

    @Test
    fun mapsProblemResponsesIntoTypedTransportFailures() = runBlocking {
        val server = HttpServer.create(InetSocketAddress(0), 0).apply {
            createContext("/api/v1/auth/device-tokens/refresh") { exchange ->
                val response = """
                    {
                      "title": "Device token refresh failed.",
                      "detail": "Refresh token is invalid or expired."
                    }
                """.trimIndent()
                exchange.sendResponseHeaders(409, response.toByteArray().size.toLong())
                exchange.responseBody.use { it.write(response.toByteArray()) }
            }
            start()
        }

        try {
            val transport = HttpDeviceRuntimeTransport("http://127.0.0.1:${server.address.port}")

            try {
                transport.refresh("refresh-token")
            } catch (exception: DeviceRuntimeTransportException) {
                assertEquals(DeviceRuntimeTransportFailureKind.Conflict, exception.kind)
                assertTrue(exception.message!!.contains("invalid or expired", ignoreCase = true))
                return@runBlocking
            }

            error("Expected DeviceRuntimeTransportException to be thrown.")
        } finally {
            server.stop(0)
        }
    }
}
