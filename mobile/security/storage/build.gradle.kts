plugins {
    alias(libs.plugins.android.library)
}

android {
    namespace = "ru.dt1520.security.authenticator.security.storage"
    compileSdk = 36

    defaultConfig {
        minSdk = 28
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }
}

kotlin {
    jvmToolchain(11)
}

dependencies {
    implementation(project(":totp-domain"))
    testImplementation(libs.junit)
}
