import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "EcoPilot AI - Carbon Twin & Coach Console",
  description: "Track, simulate, and reduce your carbon emissions with real-time AI insights powered by Gemini.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>
        {children}
      </body>
    </html>
  );
}
