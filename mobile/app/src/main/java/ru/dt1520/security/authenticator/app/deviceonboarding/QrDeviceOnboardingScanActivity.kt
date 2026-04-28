package ru.dt1520.security.authenticator.app.deviceonboarding

import android.Manifest
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Bundle
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import com.google.mlkit.vision.barcode.BarcodeScannerOptions
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.common.InputImage
import ru.dt1520.security.authenticator.core.ui.theme.DT1520AuthenticatorTheme

internal class QrDeviceOnboardingScanActivity : ComponentActivity() {
    private var hasCameraPermission by mutableStateOf(false)
    private val permissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        hasCameraPermission = granted
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        hasCameraPermission = hasCameraPermission()

        setContent {
            DT1520AuthenticatorTheme {
                if (hasCameraPermission) {
                    QrScannerScreen(
                        onPayloadScanned = ::finishWithPayload,
                        onCancel = ::finish
                    )
                } else {
                    CameraPermissionScreen(
                        onRequestPermission = {
                            permissionLauncher.launch(Manifest.permission.CAMERA)
                        },
                        onCancel = ::finish
                    )
                }
            }
        }
    }

    private fun finishWithPayload(payload: String) {
        setResult(
            RESULT_OK,
            Intent().putExtra(EXTRA_QR_PAYLOAD, payload)
        )
        finish()
    }

    private fun hasCameraPermission(): Boolean {
        return ContextCompat.checkSelfPermission(
            this,
            Manifest.permission.CAMERA
        ) == PackageManager.PERMISSION_GRANTED
    }

    companion object {
        const val EXTRA_QR_PAYLOAD = "ru.dt1520.security.authenticator.QR_PAYLOAD"

        fun createIntent(context: Context): Intent {
            return Intent(context, QrDeviceOnboardingScanActivity::class.java)
        }
    }
}

@Composable
private fun CameraPermissionScreen(
    onRequestPermission: () -> Unit,
    onCancel: () -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp, Alignment.CenterVertically),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = "Camera access is required to scan device onboarding QR.",
            style = MaterialTheme.typography.bodyLarge
        )
        Button(onClick = onRequestPermission) {
            Text("Allow camera")
        }
        Button(onClick = onCancel) {
            Text("Cancel")
        }
    }
}

@Composable
private fun QrScannerScreen(
    onPayloadScanned: (String) -> Unit,
    onCancel: () -> Unit
) {
    val context = LocalContext.current
    val scanner = rememberQrScanner(onPayloadScanned)

    Box(modifier = Modifier.fillMaxSize()) {
        AndroidView(
            factory = { viewContext ->
                PreviewView(viewContext).apply {
                    scaleType = PreviewView.ScaleType.FILL_CENTER
                    startCameraPreview(context, this, scanner)
                }
            },
            modifier = Modifier.fillMaxSize()
        )
        Button(
            onClick = onCancel,
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .padding(24.dp)
        ) {
            Text("Cancel scan")
        }
    }

    DisposableEffect(Unit) {
        onDispose {
            runCatching {
                ProcessCameraProvider.getInstance(context).get().unbindAll()
            }
        }
    }
}

@Composable
private fun rememberQrScanner(onPayloadScanned: (String) -> Unit): QrImageAnalyzer {
    val scannerOptions = BarcodeScannerOptions.Builder()
        .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
        .build()
    val barcodeScanner = BarcodeScanning.getClient(scannerOptions)

    DisposableEffect(barcodeScanner) {
        onDispose {
            barcodeScanner.close()
        }
    }

    return QrImageAnalyzer(
        scanner = barcodeScanner,
        onPayloadScanned = onPayloadScanned
    )
}

private fun startCameraPreview(
    context: Context,
    previewView: PreviewView,
    analyzer: QrImageAnalyzer
) {
    val cameraProviderFuture = ProcessCameraProvider.getInstance(context)
    cameraProviderFuture.addListener(
        {
            val cameraProvider = cameraProviderFuture.get()
            val preview = Preview.Builder().build().also { preview ->
                preview.setSurfaceProvider(previewView.surfaceProvider)
            }
            val analysis = ImageAnalysis.Builder()
                .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                .build()
            analysis.setAnalyzer(ContextCompat.getMainExecutor(context), analyzer)

            runCatching {
                cameraProvider.unbindAll()
                cameraProvider.bindToLifecycle(
                    context as ComponentActivity,
                    CameraSelector.DEFAULT_BACK_CAMERA,
                    preview,
                    analysis
                )
            }.onFailure { exception ->
                Log.w("QrDeviceScanner", "Camera binding failed.", exception)
            }
        },
        ContextCompat.getMainExecutor(context)
    )
}

private class QrImageAnalyzer(
    private val scanner: com.google.mlkit.vision.barcode.BarcodeScanner,
    private val onPayloadScanned: (String) -> Unit
) : ImageAnalysis.Analyzer {
    private var hasResult = false

    override fun analyze(imageProxy: ImageProxy) {
        if (hasResult) {
            imageProxy.close()
            return
        }

        val mediaImage = imageProxy.image
        if (mediaImage == null) {
            imageProxy.close()
            return
        }

        val image = InputImage.fromMediaImage(
            mediaImage,
            imageProxy.imageInfo.rotationDegrees
        )
        scanner.process(image)
            .addOnSuccessListener { barcodes ->
                val payload = barcodes
                    .asSequence()
                    .mapNotNull { barcode -> barcode.rawValue?.trim()?.takeIf(String::isNotBlank) }
                    .firstOrNull()
                if (payload != null && !hasResult) {
                    hasResult = true
                    onPayloadScanned(payload)
                }
            }
            .addOnFailureListener { exception ->
                Log.w("QrDeviceScanner", "QR analysis failed.", exception)
            }
            .addOnCompleteListener {
                imageProxy.close()
            }
    }
}
