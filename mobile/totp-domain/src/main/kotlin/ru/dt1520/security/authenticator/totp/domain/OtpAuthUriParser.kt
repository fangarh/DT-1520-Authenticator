package ru.dt1520.security.authenticator.totp.domain

import java.io.ByteArrayOutputStream
import java.net.URI
import kotlin.text.Charsets.UTF_8

object OtpAuthUriParser {
    fun parse(otpAuthUri: String): TotpCredential {
        val parsedUri = try {
            URI(otpAuthUri)
        } catch (_: Exception) {
            throw IllegalArgumentException("Invalid otpauth URI.")
        }

        require(parsedUri.scheme.equals("otpauth", ignoreCase = true)) {
            "Unsupported provisioning URI scheme."
        }
        require(parsedUri.host.equals("totp", ignoreCase = true)) {
            "Unsupported provisioning URI type."
        }

        val queryParameters = parseQueryParameters(parsedUri.rawQuery)
        val label = parseLabel(parsedUri.rawPath)
        val issuer = resolveIssuer(
            labelIssuer = label.issuer,
            queryIssuer = queryParameters["issuer"]?.decoded
        )
        val secret = queryParameters["secret"]?.rawValue
            ?: throw IllegalArgumentException("TOTP secret is required.")
        val digits = parseOptionalInteger(
            rawValue = queryParameters["digits"]?.decoded,
            defaultValue = TotpCredential.DEFAULT_DIGITS,
            fieldName = "digits"
        )
        val periodSeconds = parseOptionalInteger(
            rawValue = queryParameters["period"]?.decoded,
            defaultValue = 30,
            fieldName = "period"
        )

        return TotpCredential(
            account = TotpAccountDescriptor(
                issuer = issuer,
                accountName = label.accountName,
                periodSeconds = periodSeconds
            ),
            secret = TotpSecret.fromBase32(secret),
            digits = digits,
            algorithm = TotpAlgorithm.fromParameter(queryParameters["algorithm"]?.decoded)
        )
    }

    private fun parseQueryParameters(rawQuery: String?): Map<String, QueryParameter> {
        if (rawQuery.isNullOrBlank()) {
            return emptyMap()
        }

        return rawQuery.split("&")
            .filter { it.isNotBlank() }
            .associateBy(
                keySelector = { segment ->
                    val rawKey = segment.substringBefore("=")
                    val decodedKey = decodeUriComponent(rawKey).trim().lowercase()

                    require(decodedKey.isNotEmpty()) {
                        "Invalid otpauth query parameter."
                    }

                    decodedKey
                },
                valueTransform = { segment ->
                    val rawValue = segment.substringAfter("=", "")
                    QueryParameter(
                        rawValue = rawValue,
                        decoded = decodeUriComponent(rawValue).trim()
                    )
                }
            ).also { parameters ->
                require(parameters.size == rawQuery.split("&").count { it.isNotBlank() }) {
                    "Duplicate otpauth query parameter is not allowed."
                }
            }
    }

    private fun parseLabel(rawPath: String?): LabelComponents {
        val path = rawPath?.removePrefix("/")?.trim()
        require(!path.isNullOrEmpty()) {
            "Provisioning label is required."
        }

        val delimiterIndex = path.indexOf(':')
        if (delimiterIndex < 0) {
            return LabelComponents(
                issuer = null,
                accountName = decodeUriComponent(path).trim()
            )
        }

        return LabelComponents(
            issuer = decodeUriComponent(path.substring(0, delimiterIndex)).trim(),
            accountName = decodeUriComponent(path.substring(delimiterIndex + 1)).trim()
        )
    }

    private fun resolveIssuer(
        labelIssuer: String?,
        queryIssuer: String?
    ): String {
        val sanitizedLabelIssuer = labelIssuer?.takeIf(String::isNotBlank)
        val sanitizedQueryIssuer = queryIssuer?.takeIf(String::isNotBlank)

        if (sanitizedLabelIssuer != null && sanitizedQueryIssuer != null) {
            require(sanitizedLabelIssuer.equals(sanitizedQueryIssuer, ignoreCase = true)) {
                "Provisioning issuer must be consistent."
            }
        }

        return sanitizedQueryIssuer
            ?: sanitizedLabelIssuer
            ?: throw IllegalArgumentException("Provisioning issuer is required.")
    }

    private fun parseOptionalInteger(
        rawValue: String?,
        defaultValue: Int,
        fieldName: String
    ): Int {
        if (rawValue == null) {
            return defaultValue
        }

        require(rawValue.isNotBlank()) {
            "$fieldName must not be blank."
        }

        return rawValue.toIntOrNull()
            ?: throw IllegalArgumentException("$fieldName must be numeric.")
    }

    private fun decodeUriComponent(rawValue: String): String {
        val output = ByteArrayOutputStream(rawValue.length)
        var index = 0

        while (index < rawValue.length) {
            val character = rawValue[index]
            if (character != '%') {
                output.write(character.code)
                index += 1
                continue
            }

            require(index + 2 < rawValue.length) {
                "Invalid percent-encoded value."
            }

            val high = rawValue[index + 1].digitToIntOrNull(16)
            val low = rawValue[index + 2].digitToIntOrNull(16)
            require(high != null && low != null) {
                "Invalid percent-encoded value."
            }

            output.write((high shl 4) + low)
            index += 3
        }

        return output.toString(UTF_8)
    }

    private data class LabelComponents(
        val issuer: String?,
        val accountName: String
    )

    private data class QueryParameter(
        val rawValue: String,
        val decoded: String
    )
}
