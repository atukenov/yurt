import Link from "next/link";

export default function Home() {
  return (
    <div className="min-h-screen bg-linear-to-br from-amber-50 to-orange-100">
      {/* Hero Section */}
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12 md:py-24">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-12 items-center">
          {/* Left Content */}
          <div className="space-y-8">
            <div className="space-y-4">
              <h1 className="text-4xl md:text-5xl font-bold text-gray-900">
                ☕ Welcome to Yurt Coffee
              </h1>
              <p className="text-xl text-gray-600">
                Experience the perfect coffee, ordered your way. Fast, fresh,
                and always delicious.
              </p>
            </div>

            <div className="flex flex-col sm:flex-row gap-4">
              <Link
                href="/menu"
                className="px-8 py-3 bg-amber-600 text-white rounded-lg hover:bg-amber-700 transition font-semibold text-center"
              >
                Browse Menu
              </Link>
              <Link
                href="/login"
                className="px-8 py-3 border-2 border-amber-600 text-amber-600 rounded-lg hover:bg-amber-50 transition font-semibold text-center"
              >
                Sign In
              </Link>
            </div>
          </div>

          {/* Right Image */}
          <div className="text-6xl text-center">☕🍰</div>
        </div>
      </div>

      {/* Features Section */}
      <div className="bg-white py-12 md:py-24">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <h2 className="text-3xl font-bold text-center mb-12 text-gray-900">
            Why Choose Yurt Coffee?
          </h2>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
            {/* Feature 1 */}
            <div className="text-center space-y-4">
              <div className="text-4xl">⚡</div>
              <h3 className="text-xl font-bold text-gray-900">Fast Order</h3>
              <p className="text-gray-600">
                Quick and easy ordering process. Your coffee will be ready in
                minutes.
              </p>
            </div>

            {/* Feature 2 */}
            <div className="text-center space-y-4">
              <div className="text-4xl">🎯</div>
              <h3 className="text-xl font-bold text-gray-900">Customizable</h3>
              <p className="text-gray-600">
                Choose your size, toppings, and special instructions to
                personalize your drink.
              </p>
            </div>

            {/* Feature 3 */}
            <div className="text-center space-y-4">
              <div className="text-4xl">📱</div>
              <h3 className="text-xl font-bold text-gray-900">Track Orders</h3>
              <p className="text-gray-600">
                Real-time notifications on your order status from preparation to
                ready.
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* CTA Section */}
      <div className="bg-amber-600 text-white py-12 md:py-16">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
          <h2 className="text-3xl md:text-4xl font-bold mb-6">
            Ready for Your Perfect Cup?
          </h2>
          <p className="text-lg mb-8 text-amber-50">
            Join thousands of coffee lovers and order your favorite drink today.
          </p>
          <Link
            href="/menu"
            className="inline-block px-8 py-3 bg-white text-amber-600 rounded-lg hover:bg-gray-100 transition font-semibold"
          >
            Start Ordering Now
          </Link>
        </div>
      </div>
    </div>
  );
}
